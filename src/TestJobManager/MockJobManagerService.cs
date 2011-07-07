using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.IO;
using JobManagerInterfaces;

namespace TestJobManager
{
    public class MockJobManagerService
    {
        private Thread mainLoop;
        private bool running;

        private FileSystemWatcher myWatcher;

        private Queue<AutomationMessage> incoming;
        private Dictionary<Job, JobStatus> jobs;
        private List<string> lockedVMs;

        private static object IncomingQueueLock = new object();
        private static object ExecuteLock = new object();

        private static object ExecuteJobsLock = new object();
        private static object JobsDictLock = new object();

        //private bool jobsToCheck;

        private string inbox;
        private string outbox;

        public MockJobManagerService(string inbox, string outbox)
        {
            //jobsToCheck = false;
            incoming = new Queue<AutomationMessage>();
            jobs = new Dictionary<Job, JobStatus>();
            lockedVMs = new List<string>();
            mainLoop = new Thread(new ThreadStart(ReceiveLoop));
            this.inbox = inbox;
            this.outbox = outbox;
        }

        public void StartWatchingDir()
        {
            myWatcher = new FileSystemWatcher();
            myWatcher.Path = inbox;
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

        public void AddJob(Job j, JobStates state)
        {
            lock (JobsDictLock)
            {
                JobStatus js = new JobStatus();
                js.State = state;
                jobs.Add(j, js);
            }
        }

        public JobStatus GetJobStatus(string jobID)
        {
            lock (JobsDictLock)
            {
                foreach (Job j in jobs.Keys)
                {
                    if (j.JobID == jobID)
                    {
                        return jobs[j];
                    }
                }
            }
            return null;
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
                        fi.Delete();
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
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                //invalid file, rename it
                try
                {
                    fi.MoveTo(fi.FullName + ".bad");
                }
                catch (Exception) { }//eat it
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

        public void Start()
        {
            #region Handle Messages
            //look for existing files (messages)
            FileInfo[] existingFiles = new DirectoryInfo(inbox).GetFiles("*.xml");
            //start looking for new files
            StartWatchingDir();

            //handle existing files (messages)
            foreach (FileInfo fi in existingFiles)
            {
                HandleNewFile(fi);
            }
            #endregion

            //start worker loops
            running = true;
            if (!mainLoop.IsAlive)
            {
                mainLoop.Start();
            }

            //Set up background thread to periodically check jobs
            Thread backgroundChecker = new Thread(new ThreadStart(delegate
            {
                try
                {
                    while (running)
                    {
                        CheckJobs();
                        Thread.Sleep(TimeSpan.FromMinutes(1));
                    }
                }
                catch (ThreadAbortException) { }//eat it
            }));
            backgroundChecker.IsBackground = true;
            backgroundChecker.Start();

            Console.WriteLine("MockJobManagerService started successfully.");
        }

        public void Stop()
        {
            StopWatchingDir();

            lock (ExecuteLock)
            {
                running = false;
                Monitor.PulseAll(ExecuteLock);
            }
            if (!mainLoop.Join(TimeSpan.FromSeconds(5)))
            {
                mainLoop.Abort();
            }
        }

        private void CheckJobs()
        {
            lock (ExecuteJobsLock)
            {
                //jobsToCheck = true;
                Monitor.Pulse(ExecuteJobsLock);
            }
        }

        private void SendToHost(AutomationMessage m)
        {
            //m.To = hostName;
            using (TextWriter tw = new StreamWriter(Path.Combine(outbox, m.Id + ".xml")))
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
                    if (running == false)
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
                        //if (m.Content is JobCreate)
                        //{
                        //    //add job
                        //    Job j = ((JobCreate)m.Content).j;
                        //    lock (JobsDictLock)
                        //    {
                        //        jobs[j] = new JobStatus();
                        //        //set the VM path for visibility to jobmanagerconsole
                        //        if(vmMap.ContainsKey(j.Configuration))
                        //        {
                        //            jobs[j].VMPath = vmMap[j.Configuration].Path;
                        //        }
                        //    }
                        //    updateJobs = true;
                        //}
                        //else 
                        if (m.Content is SimpleRequest)
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
                                            AutomationMessage toSend = new AutomationMessage(m.From, m.Id, new JobReturn(j));
                                            SendToHost(toSend);
                                            found = true;
                                            js.State = JobStates.AutoStarted;
                                            break;
                                        }
                                    }
                                    if (!found)
                                    {
                                        AutomationMessage toSend = new AutomationMessage(m.From, m.Id, new ErrorMessage(ErrorMessage.ERROR_NO_JOB_FOUND));
                                        SendToHost(toSend);
                                    }
                                }
                            }
                            //else if (srm.Request == SimpleRequests.AllVMRequest)
                            //{
                            //    try
                            //    {
                            //        AutomationMessage toSend = new AutomationMessage(m.From, m.Id, new VMRequestReturn(VimHelper.AllVMPaths.ToArray(), lockedVMs.ToArray()));
                            //        SendToHost(toSend);
                            //    }
                            //    catch (Exception ex)
                            //    {
                            //        EventLog.WriteEntry(ex.ToString(), EventLogEntryType.Error);
                            //        AutomationMessage toSend = new AutomationMessage(m.From, m.Id, new ErrorMessage(ex.ToString()));
                            //        SendToHost(toSend);
                            //    }
                            //}
                            //else if (srm.Request == SimpleRequests.JobReport)
                            //{
                            //    lock (JobsDictLock)
                            //    {
                            //        AutomationMessage toSend = new AutomationMessage(m.From, m.Id, new JobReportReturn(jobs));
                            //        SendToHost(toSend);
                            //    }
                            //}
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
                        //else if (m.Content is JobCancelCommand)
                        //{
                        //    JobCancelCommand jcm = (JobCancelCommand)m.Content;
                        //    lock (JobsDictLock)
                        //    {
                        //        foreach (Job j in jobs.Keys)
                        //        {
                        //            if (j.JobID == jcm.JobID)
                        //            {
                        //                JobStatus js = jobs[j];
                        //                js.ErrorOut("Job was canceled by user", null, null);
                        //                updateJobs = true;
                        //                break;
                        //            }
                        //        }
                        //    }
                        //}
                        //else if (m.Content is JobDeleteCommand)
                        //{
                        //    JobDeleteCommand jdm = (JobDeleteCommand)m.Content;
                        //    lock (JobsDictLock)
                        //    {
                        //        Job toDelete = null;
                        //        foreach (Job j in jobs.Keys)
                        //        {
                        //            if (j.JobID == jdm.JobID)
                        //            {
                        //                toDelete = j;
                        //                break;
                        //            }
                        //        }
                        //        if (toDelete != null)
                        //        {
                        //            jobs.Remove(toDelete);
                        //        }
                        //    }
                        //}
                        //else if (m.Content is LockVMCommand)
                        //{
                        //    LockVMCommand cmd = (LockVMCommand)m.Content;
                        //    if (!lockedVMs.Contains(cmd.VMPath))
                        //    {
                        //        lockedVMs.Add(cmd.VMPath);
                        //    }
                        //}
                        //else if (m.Content is UnLockVMCommand)
                        //{
                        //    UnLockVMCommand cmd = (UnLockVMCommand)m.Content;
                        //    if (lockedVMs.Contains(cmd.VMPath))
                        //    {
                        //        lockedVMs.Remove(cmd.VMPath);
                        //    }
                        //}
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
                        if (running == false)
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
                Console.WriteLine("Exception in receive loop: " + ex.ToString());
                throw ex;
            }
        }
    }
}
