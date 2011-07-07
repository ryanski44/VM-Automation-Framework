using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Messaging;
using JobManagerInterfaces;
using System.Configuration;
using System.IO;

namespace JobManagerService
{
    public partial class JobManager : ServiceBase
    {
        private static TimeSpan DURATION_TO_KEEP_JOBS = TimeSpan.FromDays(AppConfig.JobLifeTimeDays);
        private static TimeSpan JOB_RUN_TIMEOUT = TimeSpan.FromMinutes(AppConfig.JobTimeoutMins);

        private Thread mainLoop;
        private Thread jobLoop;
        private bool mainLoopRunning;
        private bool jobLoopRunning;

        private DropFileManager dropManager;

        private FileSystemWatcher myWatcher;
        //private IAsyncResult result;
        //private ReceiveCompletedEventHandler receiveHandler;

        private Queue<AutomationMessage> incoming;
        private Dictionary<Job, JobStatus> jobs;
        private Dictionary<string, VirtualMachine> vmMap;
        private List<string> lockedVMs;

        private static object IncomingQueueLock = new object();
        private static object ExecuteLock = new object();

        private static object ExecuteJobsLock = new object();
        private static object JobsDictLock = new object();

        private bool jobsToCheck;

        protected IVMHostConnection vmHost;

        public JobManager()
        {
            InitializeComponent();
            jobsToCheck = false;
            incoming = new Queue<AutomationMessage>();
            jobs = new Dictionary<Job, JobStatus>();
            vmMap = new Dictionary<string, VirtualMachine>();
            lockedVMs = new List<string>();
            mainLoop = new Thread(new ThreadStart(ReceiveLoop));
            jobLoop = new Thread(new ThreadStart(JobWorkLoop));
            dropManager = new DropFileManager();
            vmHost = new VSphereHostConnection();
            //vmMap[OperatingSystemConfiguration.WinVista_32] = new VirtualMachine("DataCenter/vm/HammerVistax86", "AutoVistax86");
        }

        public void StartWatchingDir()
        {
            myWatcher = new FileSystemWatcher();
            myWatcher.Path = AppConfig.Inbox.FullName;
            myWatcher.Filter = "*.xml";
            myWatcher.IncludeSubdirectories = false;
            //myWatcher.NotifyFilter = NotifyFilters.FileName;
            myWatcher.Created += new FileSystemEventHandler(myWatcher_Created);
            myWatcher.Renamed += new RenamedEventHandler(myWatcher_Renamed);
            myWatcher.EnableRaisingEvents = true;
        }

        public void StopWatchingDir()
        {
            myWatcher.EnableRaisingEvents = false;
        }

        private void HandleNewFile(FileInfo fi)
        {
            try
            {
                bool success = false;
                while (!success)
                {
                    try
                    {
                        using (TextReader tr = new StreamReader(fi.FullName))
                        {
                            lock (IncomingQueueLock)
                            {
                                incoming.Enqueue(XMLSerializable.FromXML<AutomationMessage>(tr.ReadToEnd()));
                            }
                        }
                        success = true;
                    }
                    catch (IOException)
                    {
                        //eat it
                    }
                    catch (System.UnauthorizedAccessException)
                    {
                        //eat it
                    }
                }
                if (success && fi.Exists)
                {
                    fi.Delete();
                }
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry(ex.ToString(), EventLogEntryType.Error);
                //invalid file, rename it
                try
                {
                    fi.MoveTo(fi.FullName + ".bad");
                }
                catch (Exception ex2) 
                {
                    EventLog.WriteEntry(ex2.ToString(), EventLogEntryType.Error);
                    //eat it
                }
            }
            lock (ExecuteLock)
            {
                Monitor.PulseAll(ExecuteLock);
            }
        }

        void myWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            //a rename can happen from a different file extension or to a different file extension, as long as
            //one of the two file extensions in question is the one the filter was set up for
            //to make sure that the newly renamed file has the correct extension we check here
            if (Path.GetExtension(e.FullPath).ToLower().Equals(".xml"))
            {
                HandleNewFile(new FileInfo(e.FullPath));
            }
        }

        void myWatcher_Created(object sender, FileSystemEventArgs e)
        {
            HandleNewFile(new FileInfo(e.FullPath));
        }

        //void queue_ReceiveCompleted(object sender, ReceiveCompletedEventArgs e)
        //{
        //    //TODO catch MessageQueueException
        //    MessageQueue mq = (MessageQueue)sender;
        //    Message m = mq.EndReceive(e.AsyncResult);
            
        //    lock (IncomingQueueLock)
        //    {
        //        incoming.Enqueue(e.Message);
        //    }
        //    lock (ExecuteLock)
        //    {
        //        Monitor.PulseAll(ExecuteLock);
        //    }

        //    mq.BeginReceive();
        //}

        protected override void OnStart(string[] args)
        {
            #region Load VM Configs
            FileInfo vmConfigFile = new FileInfo(Path.Combine(AppConfig.ExecutingDir.FullName, "vm_list.txt"));
            if (vmConfigFile.Exists)
            {
                using (TextReader tr = new StreamReader(vmConfigFile.FullName))
                {
                    while (true)
                    {
                        string line = tr.ReadLine();
                        if (line == null)
                        {
                            break;
                        }
                        string[] parts = line.Split(',');
                        if (parts.Length != 4)
                        {
                            //skip the line
                            continue;
                        }
                        string config = parts[0].Trim();
                        string path = parts[1].Trim();
                        VirtualMachine vm = new VirtualMachine(parts[2].Trim(), parts[3].Trim(), vmHost.GetVMConnectionFromPath(path));
                        vmMap[config] = vm;
                    }
                }
            }
            #endregion

            #region Test Configuration
            //first check connection to ESX/vSphere
            try
            {
                vmHost.Login();
            }
            catch (System.Web.Services.Protocols.SoapException ex)
            {
                EventLog.WriteEntry("Error while logging in. Exception: " + Environment.NewLine + ex.ToString() + Environment.NewLine + ex.Detail.OuterXml, EventLogEntryType.Error);
                throw;
            }
            catch (System.Net.WebException ex)
            {
                EventLog.WriteEntry("Error while logging in. Exception: " + Environment.NewLine + ex.ToString() + Environment.NewLine + ex.Status, EventLogEntryType.Error);
                throw;
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry("Error while logging in. Exception: " + Environment.NewLine + ex.ToString(), EventLogEntryType.Error);
                throw;
            }

            //now check each VM
            foreach (VirtualMachine vm in vmMap.Values)
            {
                if (!vm.HasValidConnection)
                {
                    EventLog.WriteEntry("Error: Could not find VM \"" + vm.Identifier + "\"", EventLogEntryType.Warning);
                }
            }
            #endregion

            #region Load Jobs
            DirectoryInfo import = new DirectoryInfo(Path.Combine(AppConfig.ExecutingDir.FullName, "jobs"));
            if (import.Exists)
            {
                foreach (FileInfo jobFile in import.GetFiles("*.job"))
                {
                    try
                    {
                        Job j = null;
                        using (TextReader tr = new StreamReader(jobFile.FullName))
                        {
                            j = XMLSerializable.FromXML<Job>(tr.ReadToEnd());
                        }
                        FileInfo jobStatusFile = new FileInfo(Path.Combine(import.FullName, j.JobID + ".sta"));
                        JobStatus js = null;
                        using (TextReader tr = new StreamReader(jobStatusFile.FullName))
                        {
                            js = XMLSerializable.FromXML<JobStatus>(tr.ReadToEnd());
                        }
                        jobs[j] = js;
                    }
                    catch (Exception ex)
                    {
                        EventLog.WriteEntry(ex.ToString(), EventLogEntryType.Error);
                        continue;
                    }
                }
                import.Delete(true);
            }
            #endregion

            #region Handle Messages
            //look for existing files (messages)
            FileInfo[] existingFiles = AppConfig.Inbox.GetFiles("*.xml");
            //start looking for new files
            StartWatchingDir();

            //handle existing files (messages)
            foreach (FileInfo fi in existingFiles)
            {
                HandleNewFile(fi);
            }
            #endregion

            //start worker loops
            mainLoopRunning = true;
            if (!mainLoop.IsAlive)
            {
                mainLoop.Start();
            }

            jobLoopRunning = true;
            if (!jobLoop.IsAlive)
            {
                jobLoop.Start();
            }

            //Set up background thread to periodically check jobs
            Thread backgroundChecker = new Thread(new ThreadStart(delegate
                {
                    try
                    {
                        while (mainLoopRunning)
                        {
                            CheckJobs();
                            Thread.Sleep(TimeSpan.FromMinutes(1));
                        }
                    }
                    catch (ThreadAbortException) { }//eat it
                }));
            backgroundChecker.IsBackground = true;
            backgroundChecker.Start();
            
            EventLog.WriteEntry("JobManagerService started successfully.");
        }

        protected override void OnStop()
        {
            StopWatchingDir();

            lock (ExecuteJobsLock)
            {
                jobLoopRunning = false;
                Monitor.PulseAll(ExecuteJobsLock);
            }

            if (!jobLoop.Join(TimeSpan.FromSeconds(5)))
            {
                jobLoop.Abort();
            }

            lock (ExecuteLock)
            {
                mainLoopRunning = false;
                Monitor.PulseAll(ExecuteLock);
            }
            if (!mainLoop.Join(TimeSpan.FromSeconds(5)))
            {
                mainLoop.Abort();
            }

            DirectoryInfo export = Directory.CreateDirectory(Path.Combine(AppConfig.ExecutingDir.FullName, "jobs"));
            foreach (Job j in jobs.Keys)
            {
                JobStatus js = jobs[j];
                using (TextWriter tw = new StreamWriter(Path.Combine(export.FullName, j.JobID + ".job"), false))
                {
                    tw.Write(j.ToXML());
                }
                using (TextWriter tw = new StreamWriter(Path.Combine(export.FullName, j.JobID + ".sta"), false))
                {
                    tw.Write(js.ToXML());
                }
            }
        }

        private void CheckJobs()
        {
            lock (ExecuteJobsLock)
            {
                jobsToCheck = true;
                Monitor.Pulse(ExecuteJobsLock);
            }
        }

        private void JobWorkLoop()
        {
            try
            {
                while (true)
                {
                    //check to see if we should still be running
                    if (jobLoopRunning == false)
                    {
                        break;
                    }
                    jobsToCheck = false;
                    lock (JobsDictLock)
                    {
                        List<Job> jobsToDelete = new List<Job>();
                        foreach (Job j in jobs.Keys)
                        {
                            try
                            {
                                JobStatus js = jobs[j];
                                if (js.State == JobStates.Received)
                                {
                                    if (vmMap.ContainsKey(j.Configuration))
                                    {
                                        VirtualMachine vm = vmMap[j.Configuration];
                                        try
                                        {
                                            //check other jobs to see if any are using this VM, if so don't start this job yet
                                            //also count the number of running VMs so we can check against the max
                                            bool vmInUse = false;
                                            Dictionary<string, int> runningVMsCount = new Dictionary<string, int>();//per host
                                            foreach (Job otherJob in jobs.Keys)
                                            {
                                                JobStatus otherJobStatus = jobs[otherJob];
                                                if (otherJobStatus.IsRunning)
                                                {
                                                    VirtualMachine otherVM = vmMap[otherJob.Configuration];
                                                    string computeResource = otherVM.ComputeResourceName;
                                                    if (!runningVMsCount.ContainsKey(computeResource))
                                                    {
                                                        runningVMsCount[computeResource] = 0;
                                                    }
                                                    runningVMsCount[computeResource]++;
                                                    if (vm.Identifier == otherVM.Identifier)
                                                    {
                                                        vmInUse = true;
                                                    }
                                                }
                                            }

                                            //check to see if the vm is locked
                                            foreach (string lockedVMPath in lockedVMs)
                                            {
                                                if (lockedVMPath == vm.Identifier)
                                                {
                                                    vmInUse = true;
                                                    break;
                                                }
                                            }

                                            //if this job relies on another job, make sure that job has finished before starting this one
                                            bool waitingForAnotherJob = false;
                                            if (j.DependsOnJobIds != null && j.DependsOnJobIds.Count > 0)
                                            {
                                                foreach (string jobId in j.DependsOnJobIds)
                                                {
                                                    bool jobFinished = false;
                                                    foreach (Job baseJob in jobs.Keys)
                                                    {
                                                        JobStatus baseJobStatus = jobs[baseJob];
                                                        if (baseJob.JobID == jobId)
                                                        {
                                                            if (baseJobStatus.State == JobStates.WaitingForChildJobs)
                                                            {
                                                                jobFinished = true;
                                                            }
                                                        }
                                                    }
                                                    if (!jobFinished)
                                                    {
                                                        waitingForAnotherJob = true;
                                                    }
                                                }
                                            }
                                            int vmsRunningOnThisResource = 0;
                                            string thisComputeResource = vm.ComputeResourceName;
                                            if (runningVMsCount.ContainsKey(thisComputeResource))
                                            {
                                                vmsRunningOnThisResource = runningVMsCount[thisComputeResource];
                                            }
                                            if (!vmInUse && !waitingForAnotherJob && vmsRunningOnThisResource < AppConfig.MaxVMsAtOnce)
                                            {
                                                //copy ISO to drop directory
                                                List<string> keys = new List<string>(j.ISOs.Keys);
                                                foreach (string isoName in keys)
                                                {
                                                    string isoPath = j.ISOs[isoName];
                                                    if (File.Exists(isoPath))
                                                    {
                                                        string dropFile = dropManager.GetDropFilePath(isoPath, isoPath);
                                                        j.ISOs[isoName] = dropFile;
                                                    }
                                                    else
                                                    {
                                                        //TODO error?
                                                    }
                                                }
                                                if (j.Packages == null || j.Packages.Count == 0)
                                                {
                                                    js.ErrorOut("Job does not have any packages defined", null, null);
                                                }
                                                else
                                                {
                                                    //copy Test Files to readable dir
                                                    foreach (ExecutablePackage ep in j.Packages)
                                                    {
                                                        DirectoryInfo sourceDir = new DirectoryInfo(ep.ContentDirectory);
                                                        DirectoryData testFiles = DirectoryData.FromDirectory(sourceDir);
                                                        DirectoryInfo destDir = new DirectoryInfo(AppConfig.FileDrop.FullName + "\\" + Guid.NewGuid().ToString());
                                                        destDir.Create();
                                                        testFiles.DumpContentsToDir(destDir);

                                                        foreach (string subDirName in ep.SubContentDirectories.Keys)
                                                        {
                                                            DirectoryInfo subDirSource = new DirectoryInfo(ep.SubContentDirectories[subDirName]);
                                                            DirectoryData subDirFiles = DirectoryData.FromDirectory(subDirSource);
                                                            DirectoryInfo subDirDest = new DirectoryInfo(Path.Combine(destDir.FullName, subDirName));
                                                            subDirDest.Create();
                                                            subDirFiles.DumpContentsToDir(subDirDest);
                                                        }

                                                        ep.ContentDirectory = destDir.FullName;
                                                    }

                                                    vm.RevertToNamedSnapshot();
                                                    //vm.RevertToCurrentSnapshot();

                                                    vm.Start();

                                                    js.State = JobStates.VMStarted;
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            List<FileData> attachements = new List<FileData>();
                                            FileData exceptionDetails = new FileData();
                                            string exceptionDetailsStr = ex.ToString();
                                            if (ex is System.Web.Services.Protocols.SoapException)
                                            {
                                                System.Web.Services.Protocols.SoapException soapEx = (System.Web.Services.Protocols.SoapException)ex;
                                                if (soapEx.Detail != null)
                                                {
                                                    exceptionDetailsStr += Environment.NewLine + soapEx.Detail.OuterXml;
                                                }
                                            }
                                            exceptionDetails.Data = Encoding.ASCII.GetBytes(exceptionDetailsStr);
                                            exceptionDetails.Name = "exception.txt";
                                            attachements.Add(exceptionDetails);
                                            js.ErrorOut("Exception: " + ex.Message, null, attachements);
                                        }
                                    }
                                    else
                                    {
                                        js.ErrorOut("Could not find a VM suitable for this configuration(" + j.Configuration.ToString() + ")", null, null);
                                    }
                                }
                                else if (js.State == JobStates.VMStarted || js.State == JobStates.AutoStarted)
                                {
                                    if (DateTime.Now.Subtract(js.LastStateChange) > JOB_RUN_TIMEOUT)
                                    {
                                        js.ErrorOut("Job timed out. No response from VM after " + JOB_RUN_TIMEOUT.TotalHours + " hours", null, null);
                                    }
                                }
                                else if (js.State == JobStates.AutoFinished)
                                {
                                    //check to see if any jobs rely on this job
                                    bool hasChildJobs = false;
                                    foreach (Job other in jobs.Keys)
                                    {
                                        if (other.DependsOnJobIds != null && other.DependsOnJobIds.Contains(j.JobID))//if the other job relies on this job
                                        {
                                            hasChildJobs = true;
                                            break;
                                        }
                                    }
                                    if (hasChildJobs)
                                    {
                                        js.State = JobStates.WaitingForChildJobs;
                                        jobsToCheck = true;
                                    }
                                    else if (js.Result.SnapshotOnShutdown)
                                    {
                                        js.State = JobStates.TakingSnapshot;
                                    }
                                    else
                                    {
                                        js.State = JobStates.JobFinishedNotSent;
                                    }
                                }
                                else if (js.State == JobStates.TakingSnapshot)
                                {
                                    VirtualMachine vm = vmMap[j.Configuration];
                                    if (vm.IsStarted)
                                    {
                                        //VM is still shuting down, do nothing, this will get checked again on the next go around.
                                    }
                                    else
                                    {
                                        string snapshotName = vm.SnapshotName;
                                        if (!String.IsNullOrEmpty(js.Result.SnapshotName))
                                        {
                                            snapshotName = js.Result.SnapshotName;
                                        }
                                        string snapshotDesc = String.Empty;
                                        if (js.Result.SnapshotDesc != null)
                                        {
                                            snapshotDesc = js.Result.SnapshotDesc;
                                        }
                                        vm.TakeSnapshot(snapshotName, snapshotDesc);
                                        if (js.Result.CloneOnShutdown)
                                        {
                                            try
                                            {
                                                vm.CreateLinkedClone(snapshotName, vm.VMName + "_" + snapshotName);
                                            }
                                            catch (Exception ex)
                                            {
                                                js.ErrorOut("Exception: " + ex.Message, null, null);
                                            }
                                        }
                                        js.State = JobStates.JobFinishedNotSent;
                                    }
                                }
                                else if (js.State == JobStates.WaitingForChildJobs)
                                {
                                    bool inUse = false;
                                    foreach (Job other in jobs.Keys)
                                    {
                                        if (other.DependsOnJobIds != null && other.DependsOnJobIds.Contains(j.JobID))//if the other job relies on this job
                                        {
                                            JobStatus otherStatus = jobs[other];
                                            if (!otherStatus.IsFinished)
                                            {
                                                inUse = true;
                                                break;
                                            }
                                        }
                                    }
                                    if (!inUse)
                                    {
                                        if (js.Result.SnapshotOnShutdown)
                                        {
                                            js.State = JobStates.TakingSnapshot;
                                        }
                                        else
                                        {
                                            js.State = JobStates.JobFinishedNotSent;
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                //TODO
                                throw;
                            }
                        }

                        foreach (Job j in jobs.Keys)
                        {
                            JobStatus js = jobs[j];
                            if (js.State == JobStates.JobFinishedNotSent)
                            {
                                AutomationMessage m = new AutomationMessage(j.OriginalHost, j.OriginalMessageID,  new JobCompleted(j, js.Result));
                                SendToHost(m);

                                js.State = JobStates.JobFinishedSent;

                                //if the iso has been copied to the temp directory, delete it
                                if (j.ISOs != null)
                                {
                                    foreach (string isoPath in j.ISOs.Values)
                                    {
                                        if (isoPath.Contains(AppConfig.FileDrop.FullName))
                                        {
                                            try
                                            {
                                                //release the iso copy
                                                dropManager.ReleaseDropFile(isoPath);
                                            }
                                            catch (Exception ex)
                                            {
                                                EventLog.WriteEntry("Could not release ISO \"" + isoPath + "\" : " + ex.ToString());
                                            }
                                        }
                                    }
                                }
                                
                                //if the test package directory has been copied to the temp directory, delete it
                                if (j.Packages != null)
                                {
                                    foreach (ExecutablePackage ep in j.Packages)
                                    {
                                        string packageDir = ep.ContentDirectory;
                                        if (packageDir != null)
                                        {
                                            if (packageDir.Contains(AppConfig.FileDrop.FullName))
                                            {
                                                try
                                                {
                                                    //delete the test files
                                                    System.IO.Directory.Delete(packageDir, true);
                                                }
                                                catch (Exception ex)
                                                {
                                                    EventLog.WriteEntry("Could not delete directory \"" + packageDir + "\" : " + ex.ToString());
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            else if (js.State == JobStates.JobFinishedSent)
                            {
                                if (DateTime.Now.Subtract(js.LastStateChange) > DURATION_TO_KEEP_JOBS)
                                {
                                    jobsToDelete.Add(j);
                                }
                            }
                        }

                        foreach (Job j in jobsToDelete)
                        {
                            jobs.Remove(j);
                        }
                    }

                    lock (ExecuteJobsLock)
                    {
                        if (jobsToCheck)
                        {
                            continue;
                        }

                        //check to see if we should still be running
                        if (jobLoopRunning == false)
                        {
                            break;
                        }
                        
                        Monitor.Wait(ExecuteJobsLock);
                    }
                }
            }
            catch (ThreadAbortException)
            {
                //eat it
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry("Exception in work loop: " + ex.ToString(), EventLogEntryType.Error);
                throw;
            }
        }

        private void SendToHost(AutomationMessage m)
        {
            //m.To = hostName;
            using (TextWriter tw = new StreamWriter(Path.Combine(AppConfig.Outbox.FullName, m.Id + ".xml")))
            {
                tw.Write(m.ToXML());
            }
            //using (MessageQueue mq = MessageSendRecieve.GetSpecificMessageQueue(@"FormatName:DIRECT=OS:" + hostName + @"\Private$\" + MessageSendRecieve.LocalQueueName))
            //{
            //    mq.Send(m, MessageQueueTransactionType.Single);
            //}
        }

        private void ReceiveLoop()
        {
            try
            {
                while (true)
                {
                    //check to see if we need to stop
                    if (mainLoopRunning == false)
                    {
                        break;
                    }

                    List<AutomationMessage> newMessages = new List<AutomationMessage>();
                    lock (IncomingQueueLock)
                    {
                        while (incoming.Count > 0)
                        {
                            newMessages.Add(incoming.Dequeue());
                        }
                    }

                    bool updateJobs = false;

                    foreach (AutomationMessage m in newMessages)
                    {
                        try
                        {
                            if (m.Content is JobCreate)
                            {
                                //add job
                                Job j = ((JobCreate)m.Content).j;
                                JobStatus js = new JobStatus();

                                if (j.ISOs == null)
                                {
                                    j.ISOs = new SerializableDictionary<string, string>();
                                }

                                if (j.Properties == null)
                                {
                                    j.Properties = new SerializableDictionary<string, string>();
                                }

                                lock (JobsDictLock)
                                {
                                    jobs[j] = js;
                                    //check required fields
                                    if (String.IsNullOrEmpty(j.Configuration))
                                    {
                                        js.ErrorOut("Configuration cannot be null or empty", null, null);
                                    }
                                    else if (vmMap.ContainsKey(j.Configuration))
                                    {
                                        //set the VM path for visibility to jobmanagerconsole
                                        jobs[j].VMPath = vmMap[j.Configuration].Identifier;
                                    }
                                }

                                updateJobs = true;
                            }
                            else if (m.Content is SimpleRequest)
                            {
                                SimpleRequest srm = (SimpleRequest)m.Content;
                                if (srm.Request == SimpleRequests.JobRequest)
                                {
                                    lock (JobsDictLock)
                                    {
                                        bool found = false;
                                        foreach (Job j in jobs.Keys)
                                        {
                                            JobStatus js = jobs[j];
                                            if (js.State == JobStates.VMStarted)
                                            {
                                                VirtualMachine vm = vmMap[j.Configuration];
                                                if (vm.IsSameHost(m.From))
                                                {
                                                    AutomationMessage toSend = new AutomationMessage(vm.HostName, m.Id, new JobReturn(j));
                                                    SendToHost(toSend);
                                                    found = true;
                                                    js.State = JobStates.AutoStarted;
                                                    break;
                                                }
                                            }
                                        }
                                        if (!found)
                                        {
                                            AutomationMessage toSend = new AutomationMessage(m.From, m.Id, new ErrorMessage(ErrorMessage.ERROR_NO_JOB_FOUND));
                                            SendToHost(toSend);
                                        }
                                    }
                                }
                                else if (srm.Request == SimpleRequests.AllVMRequest)
                                {
                                    try
                                    {
                                        AutomationMessage toSend = new AutomationMessage(m.From, m.Id, new VMRequestReturn(vmHost.AllVMIdentifiers.ToArray(), lockedVMs.ToArray()));
                                        SendToHost(toSend);
                                    }
                                    catch (Exception ex)
                                    {
                                        EventLog.WriteEntry(ex.ToString(), EventLogEntryType.Error);
                                        AutomationMessage toSend = new AutomationMessage(m.From, m.Id, new ErrorMessage(ex.ToString()));
                                        SendToHost(toSend);
                                    }
                                }
                                else if (srm.Request == SimpleRequests.JobReport)
                                {
                                    lock (JobsDictLock)
                                    {
                                        AutomationMessage toSend = new AutomationMessage(m.From, m.Id, new JobReportReturn(jobs));
                                        SendToHost(toSend);
                                    }
                                }
                            }
                            else if (m.Content is JobCompleted)
                            {
                                JobCompleted jcm = (JobCompleted)m.Content;
                                lock (JobsDictLock)
                                {
                                    foreach (Job j in jobs.Keys)
                                    {
                                        if (j.JobID == jcm.Job.JobID)
                                        {
                                            JobStatus js = jobs[j];
                                            js.Result = jcm.Result;
                                            js.State = JobStates.AutoFinished;
                                            updateJobs = true;
                                        }
                                    }
                                }
                            }
                            else if (m.Content is JobCancelCommand)
                            {
                                JobCancelCommand jcm = (JobCancelCommand)m.Content;
                                lock (JobsDictLock)
                                {
                                    foreach (Job j in jobs.Keys)
                                    {
                                        if (j.JobID == jcm.JobID)
                                        {
                                            JobStatus js = jobs[j];
                                            js.ErrorOut("Job was canceled by user", null, null);
                                            updateJobs = true;
                                            break;
                                        }
                                    }
                                }
                            }
                            else if (m.Content is JobDeleteCommand)
                            {
                                JobDeleteCommand jdm = (JobDeleteCommand)m.Content;
                                lock (JobsDictLock)
                                {
                                    Job toDelete = null;
                                    foreach (Job j in jobs.Keys)
                                    {
                                        if (j.JobID == jdm.JobID)
                                        {
                                            toDelete = j;
                                            break;
                                        }
                                    }
                                    if (toDelete != null)
                                    {
                                        jobs.Remove(toDelete);
                                    }
                                }
                            }
                            else if (m.Content is LockVMCommand)
                            {
                                LockVMCommand cmd = (LockVMCommand)m.Content;
                                if (!lockedVMs.Contains(cmd.VMPath))
                                {
                                    lockedVMs.Add(cmd.VMPath);
                                }
                            }
                            else if (m.Content is UnLockVMCommand)
                            {
                                UnLockVMCommand cmd = (UnLockVMCommand)m.Content;
                                if (lockedVMs.Contains(cmd.VMPath))
                                {
                                    lockedVMs.Remove(cmd.VMPath);
                                }
                            }
                            else
                            {
                                EventLog.WriteEntry("Unknown message type " + m.Content.GetType().ToString(), EventLogEntryType.Error);
                                string badFilePath = Path.Combine(AppConfig.Inbox.FullName, m.Id + ".xml.bad");
                                File.WriteAllText(badFilePath, m.ToXML(true));
                            }
                        }
                        catch (Exception ex)
                        {
                            string badFilePath = Path.Combine(AppConfig.Inbox.FullName, m.Id + ".xml.bad");
                            EventLog.WriteEntry(string.Format("Exception while processing message. Message saved to \"{0}\". Exception: {1}", badFilePath, ex.ToString()), EventLogEntryType.Error);
                            File.WriteAllText(badFilePath, m.ToXML(true));
                        }
                    }
                    if (updateJobs)
                    {
                        CheckJobs();
                    }

                    lock (ExecuteLock)
                    {
                        //check for more items
                        lock (IncomingQueueLock)
                        {
                            if (incoming.Count > 0)
                            {
                                continue;
                            }
                        }
                        //check to see if we should still be running
                        if (mainLoopRunning == false)
                        {
                            break;
                        }
                        Monitor.Wait(ExecuteLock);
                    }
                }
            }
            catch (ThreadAbortException)
            {
                //eat it
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry("Exception in receive loop: " + ex.ToString());
                throw;
            }
        }
    }
}
