using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using JobManagerInterfaces;
using System.IO;
using System.Management;
using System.Security.Principal;
using System.Diagnostics;
using System.Threading;

namespace JobManagerClient
{
    public class JobClient : JobClientInterface
    {
        private static TimeSpan DEFAULT_JOB_WAIT = TimeSpan.FromHours(2);

        private bool startupEntryAdded;
        private static Assembly LoadedJobRunnerAssembly = null;
        private Thread workerThread;
        private DirectoryInfo baseDir;
        private DirectoryInfo runningPackageDir;
        private ExecutablePackage runningPackage;
        private Job j;
        public bool ShutdownOnCompletion { get; set; }
        private ISystem system;

        public JobClient(ISystem localSystem)
        {
            this.system = localSystem;
            startupEntryAdded = false;
            ShutdownOnCompletion = false;
            j = null;
            baseDir = new DirectoryInfo(Path.Combine(Utilities.ExecutingAssembly.Directory.FullName, "TestDLL"));
            workerThread = new Thread(new ThreadStart(Run));
            
            workerThread.IsBackground = true;
        }

        public void Start()
        {
            if (!workerThread.IsAlive)
            {
                workerThread.Start();
            }
        }

        public void Stop()
        {
            if (workerThread.IsAlive)
            {
                workerThread.Abort();
            }
            if (workerThread.IsAlive)
            {
                workerThread.Join(2000);
            }
        }

        public void WaitForExit()
        {
            if (workerThread.IsAlive)
            {
                workerThread.Join();
            }
        }

        public Job RunningJob
        {
            get
            {
                return j;
            }
        }

        public DirectoryInfo WorkingDir
        {
            get { return runningPackageDir; }
        }

        public DriveInfo MountISO(string UNCPath)
        {
            DriveInfo di = null;
            string currentISO = null;

            #region find magic disc drive and contents

            Process p = new Process();
            p.StartInfo.FileName = AppConfig.MISOPath;
            p.StartInfo.Arguments = "NULL -vlist";
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.Start();
            p.WaitForExit();

            Regex findDriveSearchPattern = new Regex(@"\[\d\]\s+([^(]*)\((\w:)\)"); // matches this type of string: "[1] No Media   (G:)" and pulls out "G:"

            string line = String.Empty;
            while ((line = p.StandardOutput.ReadLine()) != null)
            {
                Match m = findDriveSearchPattern.Match(line);
                if (m.Success)
                {
                    currentISO = m.Groups[1].Value;
                    di = new DriveInfo(m.Groups[2].Value);
                }
            }
            #endregion

            if (currentISO.Trim().ToLower() != UNCPath.Trim().ToLower())
            {
                //mount
                string args = "NULL -mnt " + di.RootDirectory.Name + " \"" + UNCPath + "\"";
                Logger.Instance.LogString("Running '" + AppConfig.MISOPath + " " + args + "'");
                Process.Start(AppConfig.MISOPath, args).WaitForExit();
            }
            return di;
        }

        public string GetPropertyValue(string key)
        {
            //look in the package properties first
            if (runningPackage != null)
            {
                if (runningPackage.Properties.ContainsKey(key))
                {
                    return runningPackage.Properties[key];
                }
            }
            //then look in the job properties
            if (j.Properties.ContainsKey(key))
            {
                return j.Properties[key];
            }
            else
            {
                return null;
            }
        }

        public void SetPropertyValue(string key, string value)
        {
            j.Properties[key] = value;
        }

        public void LogString(string text)
        {
            if (!String.IsNullOrEmpty(text))
            {
                Logger.Instance.LogString(text);
                if (text.Contains(Environment.NewLine))
                {
                    system.UpdateStatus(text.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)[0], false);
                }
                else
                {
                    system.UpdateStatus(text, false);
                }
            }
        }

        public void StartupOnNextRun()
        {
            if (!startupEntryAdded)
            {
                Logger.Instance.LogString("Putting in Startup Entry");
                Utilities.AddStartEntry("  ainstallAutomation", "\"" + Assembly.GetExecutingAssembly().Location + "\" postrestart");

                startupEntryAdded = true;
            }
            using (TextWriter tw = new StreamWriter(Path.Combine(Utilities.ExecutingAssembly.Directory.FullName, "job.xml"), false))
            {
                tw.Write(j.ToXML());
            }
        }

        public void Run()
        {
            try
            {
                FileInfo mapNetBatchFile = new FileInfo(Path.Combine(Utilities.ExecutingAssembly.Directory.FullName, "map.bat"));
                //if the network drive is disconected, then we will be unable to get to the server inbox, in which case we should try to remap
                if (!AppConfig.ServerInbox.Exists && mapNetBatchFile.Exists)
                {
                    using (System.Diagnostics.Process p = new System.Diagnostics.Process())
                    {
                        p.StartInfo.WorkingDirectory = mapNetBatchFile.Directory.FullName;
                        p.StartInfo.FileName = mapNetBatchFile.Name;
                        p.StartInfo.CreateNoWindow = true;
                        p.StartInfo.UseShellExecute = true;
                        p.Start();
                        p.WaitForExit();
                    }
                }
                int packageToRun = 0;
                JobResult result = new JobResult();
                result.Completed = true;

                //string sendQueueName = @"FormatName:DIRECT=OS:hammerbuildvm\Private$\jobmanager";
                //string sendQueueName = @"FormatName:DIRECT=OS:ryanadams2\Private$\test2";
                //jci.LogString("Connecting to Job Manager receive queue (" + sendQueueName + ")");
                MessageSendRecieve msr = new MessageSendRecieve(AppConfig.ServerInbox, AppConfig.ServerOutbox);
                //jci.LogString("Permission = " + msr.RemoteMessageQueue.AccessMode.ToString());

                //look for an existing job to run/continue before getting a new job from the server
                FileInfo jobXML = new FileInfo(Path.Combine(Utilities.ExecutingAssembly.Directory.FullName, "job.xml"));
                if (jobXML.Exists)
                {
                    using (TextReader tr = new StreamReader(jobXML.FullName))
                    {
                        j = XMLSerializable.FromXML<Job>(tr.ReadToEnd());
                        if (j.Properties.ContainsKey("PackageToRun"))
                        {
                            packageToRun = Int32.Parse(j.Properties["PackageToRun"]);
                        }
                    }
                    try
                    {
                        //rename the job file so the next run doesn't automatically use it.  The job.xml file will be put back
                        //as part of jci.StartupOnNextRun if it is meant to be continued after a restart
                        string lastFile = jobXML.FullName + ".old";
                        if(File.Exists(lastFile))
                        {
                            File.Delete(lastFile);
                        }
                        File.Move(jobXML.FullName, lastFile);
                    }
                    catch (Exception ex)
                    {
                        //if the delete fails lets log it, but it isn't critical so let's eat the exception
                        LogString("Could not delete existing job.xml file: " + ex.ToString());
                    }
                    //look for an existing JobResult to pull in
                    FileInfo jobResultXML = new FileInfo(Path.Combine(Utilities.ExecutingAssembly.Directory.FullName, "jobresult.xml"));
                    if (jobResultXML.Exists)
                    {
                        try
                        {
                            using (TextReader tr = new StreamReader(jobResultXML.FullName))
                            {
                                result = XMLSerializable.FromXML<JobResult>(tr.ReadToEnd());
                            }
                        }
                        catch (Exception ex)
                        {
                            //log, but eat it
                            LogString(ex.ToString());
                        }
                    }
                }
                else
                {
                    LogString("Requesting Jobs from Job Manager");
                    string messageID = msr.RequestJob();
                    LogString("Sent request with message id: " + messageID);

                    LogString("Waiting for Job response from Job Manager");
                    j = msr.WaitForJob(messageID, DEFAULT_JOB_WAIT);
                    if (j == null)
                    {
                        LogString("No Jobs Available");
                        return;
                    }
                    try
                    {
                        LogString("Found Job: " + j.JobID);

                        if (baseDir.Exists)
                        {
                            baseDir.Delete(true);
                            //TODO wait for files to be deleted?
                        }
                        baseDir.Create();

                        List<string> keys = new List<string>(j.ISOs.Keys);
                        foreach (string isoName in keys)
                        {
                            FileInfo isoPath = new FileInfo(j.ISOs[isoName]);
                            string destPath = Path.Combine(Utilities.ExecutingAssembly.Directory.FullName, isoPath.Name);
                            LogString("Copying ISO from \"" + isoPath.Directory.FullName + "\" to \"" + destPath + "\"");
                            isoPath.CopyTo(destPath);
                            j.ISOs[isoName] = destPath;
                        }

                        if (j.Properties == null)
                        {
                            j.Properties = new SerializableDictionary<string, string>();
                        }
                    }
                    catch (Exception ex)
                    {
                        LogString(ex.ToString());
                        result.Completed = false;
                        ExecutionResult er = new ExecutionResult(ex.ToString(), null);
                        result.ExecutionResults.Add(er);
                        Logger.Instance.Pause();
                        result.Logs.Add(FileData.FromFile(new FileInfo(Logger.Instance.FileName)));
                        Logger.Instance.Resume();
                        LogString("Sending Job Result");
                        msr.ReportJobStatus(new JobCompleted(j, result));
                        LogString("Job Result Sent");
                        return;
                    }
                }
                if (j.Packages.Count == 0)
                {
                    Logger.Instance.Pause();
                    result.Logs.Add(FileData.FromFile(new FileInfo(Logger.Instance.FileName)));
                    Logger.Instance.Resume();
                }
                while (packageToRun < j.Packages.Count)
                {
                    runningPackageDir = new DirectoryInfo(Path.Combine(baseDir.FullName, packageToRun.ToString()));

                    ExecutablePackage ep = j.Packages[packageToRun];
                    runningPackage = ep;
                    ExecutionResult er = new ExecutionResult();
                    try
                    {
                        if (!ep.ContentDirectory.ToLower().Equals(runningPackageDir.FullName.ToLower()))
                        {
                            if (runningPackageDir.Exists)
                            {
                                runningPackageDir.Delete(true);
                            }
                            runningPackageDir.Create();

                            LogString("Copying data from \"" + ep.ContentDirectory + "\" to \"" + runningPackageDir.FullName + "\"");
                            DirectoryData.FromDirectory(new DirectoryInfo(ep.ContentDirectory)).DumpContentsToDir(runningPackageDir);
                            ep.ContentDirectory = runningPackageDir.FullName;
                        }
                        LogString("Loading external test DLL: " + ep.JobRunnerDLLName + " , " + ep.JobRunnerClassName);
                        JobRunner jr = LoadJobRunner(ep.JobRunnerClassName, Path.Combine(runningPackageDir.FullName, ep.JobRunnerDLLName));

                        LogString("Executing Execute() method on external DLL");

                        er = jr.Execute(this);
                    }
                    catch (Exception ex)
                    {
                        LogString(ex.ToString());
                        result.Completed = false;
                        er = new ExecutionResult(ex.ToString(), null);
                    }

                    Logger.Instance.Pause();
                    result.Logs.Add(FileData.FromFile(new FileInfo(Logger.Instance.FileName)));
                    Logger.Instance.Resume();

                    if (er != null)
                    {
                        result.ExecutionResults.Add(er);
                    }

                    //lets save the current job result
                    using (TextWriter tw = new StreamWriter(Path.Combine(Utilities.ExecutingAssembly.Directory.FullName, "jobresult.xml"), false))
                    {
                        tw.Write(result.ToXML());
                    }

                    if (er == null)
                    {
                        //The automation is likely not finished, the computer is likely going to reboot and 
                        //we want this execution to continue after reboot so we should exit now instead of going to the next package.
                        //the executable package should have already called startuponnextrun
                        return;
                    }
                    

                    if (!er.Success)
                    {
                        //stop on first error
                        break;
                    }

                    packageToRun++;
                    j.Properties["PackageToRun"] = packageToRun.ToString();
                    if (er.Success && er.RestartAfter)
                    {
                        StartupOnNextRun();
                        LogString("Restarting ...");
                        system.Shutdown(true);
                        return;
                    }
                }
                LogString("Sending Job Result");
                msr.ReportJobStatus(new JobCompleted(j, result));
                LogString("Job Result Sent");
                //cleanup
                if (File.Exists(Path.Combine(Utilities.ExecutingAssembly.Directory.FullName, "jobresult.xml")))
                {
                    File.Delete(Path.Combine(Utilities.ExecutingAssembly.Directory.FullName, "jobresult.xml"));
                }
                if (ShutdownOnCompletion)
                {
                    LogString("Shuting Down ...");
                    system.Shutdown(false);

                    //so, lets exit the program
                    System.Windows.Forms.Application.Exit();
                }
            }
            catch (ThreadAbortException)
            {
                //eat it, get out right away.  Program is exiting or user has stopped automation
                return;
            }
            catch (Exception e)
            {
                LogString("Exception in thread: "+e.ToString());
                return;
            }
        }

        static JobRunner LoadJobRunner(string title_class, string dllPath)
        {
            LoadedJobRunnerAssembly = Assembly.LoadFrom(dllPath);
            JobRunner jr = (JobRunner)LoadedJobRunnerAssembly.CreateInstance(title_class);
            if (jr == null)
            {
                throw new Exception("method/class not found");
            }
            return jr;
        }

        
    }
}
