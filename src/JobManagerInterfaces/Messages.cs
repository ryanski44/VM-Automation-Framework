using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace JobManagerInterfaces
{

    public class MessageSendRecieve
    {
        private DirectoryInfo serverInbox;
        private DirectoryInfo receiveInbox;

        public MessageSendRecieve(DirectoryInfo serverInbox, DirectoryInfo receiveInbox)
        {
            this.serverInbox = serverInbox;
            this.receiveInbox = receiveInbox;
        }

        public AutomationMessage WaitForMessage(string messageID, TimeSpan maxWait)
        {
            DateTime startedWaiting = DateTime.Now;

            bool found = false;
            AutomationMessage message = null;
            object messageLock = new object();

            FileSystemWatcher myWatcher = new FileSystemWatcher();
            myWatcher.Path = receiveInbox.FullName;
            myWatcher.Filter = messageID + ".xml";
            myWatcher.IncludeSubdirectories = false;
            //myWatcher.NotifyFilter = NotifyFilters.FileName;
            myWatcher.Created += new FileSystemEventHandler(delegate(object sender, FileSystemEventArgs e)
                {
                    lock (messageLock)
                    {
                        if (!found)
                        {
                            message = GetAutomationMessageFromFile(new FileInfo(e.FullPath), TimeSpan.FromMinutes(1));
                            found = true;
                            myWatcher.EnableRaisingEvents = false;
                        }
                    }
                });
            myWatcher.EnableRaisingEvents = true;
            //the file may already be in the directory before we had time to get the watcher working, so lets check now
            FileInfo expectedFile = new FileInfo(Path.Combine(receiveInbox.FullName, messageID + ".xml"));
            lock (messageLock)
            {
                if (!found)
                {
                    if (expectedFile.Exists)
                    {
                        message = GetAutomationMessageFromFile(expectedFile, TimeSpan.FromMinutes(1));
                        found = true;
                        myWatcher.EnableRaisingEvents = false;
                    }
                }
            }
            while (!found)
            {
                if (DateTime.Now.Subtract(startedWaiting) > maxWait)
                {
                    return null;
                }
                System.Threading.Thread.Sleep(1000);
            }
            myWatcher.Dispose();
            return message;
        }

        private static AutomationMessage GetAutomationMessageFromFile(FileInfo fi, TimeSpan timeout)
        {
            AutomationMessage message = null;
            bool success = false;
            DateTime end = DateTime.Now.Add(timeout);
            while (!success && DateTime.Now < end)
            {
                try
                {
                    using (TextReader tr = new StreamReader(fi.FullName))
                    {
                        message = XMLSerializable.FromXML<AutomationMessage>(tr.ReadToEnd());
                    }
                    success = true;
                    fi.Delete();
                }
                catch (Exception)
                {
                    //eat it
                }
            }
            return message;
        }

        public string Send(AutomationMessage m)
        {
            using (TextWriter tw = new StreamWriter(Path.Combine(serverInbox.FullName, m.Id + ".xml")))
            {
                tw.Write(m.ToXML());
            }
            return m.Id;
        }

        public void SendToHost(AutomationMessage m)
        {
            using (TextWriter tw = new StreamWriter(Path.Combine(receiveInbox.FullName, m.Id + ".xml")))
            {
                tw.Write(m.ToXML());
            }
        }

        public string QueueJob(Job j)
        {
            AutomationMessage m = new AutomationMessage(new JobCreate(j));
            j.OriginalMessageID = m.Id;
            return Send(m);
        }

        public JobCompleted WaitForJobCompletion(string messageID, TimeSpan maxWait)
        {
            AutomationMessage m = WaitForMessage(messageID, maxWait);
            if (m == null) { return null; }
            if(m.Content is JobCompleted)
            {
                return (JobCompleted)m.Content;
            }
            return null;
        }

        public string RequestJob()
        {
            AutomationMessage m = new AutomationMessage(new SimpleRequest(SimpleRequests.JobRequest));
            return Send(m);
        }

        public Job WaitForJob(string msgID, TimeSpan maxWait)
        {
            AutomationMessage m = WaitForMessage(msgID, maxWait);
            if (m == null) { return null; }
            if (m.Content is JobReturn)
            {
                return ((JobReturn)m.Content).j;
            }
            else if (m.Content is ErrorMessage)
            {
                if (((ErrorMessage)m.Content).Error == ErrorMessage.ERROR_NO_JOB_FOUND)
                {
                    return null;
                }
            }
            throw new NotImplementedException();
        }

        public VMRequestReturn WaitForVMList(string msgID, TimeSpan maxWait)
        {
            AutomationMessage m = WaitForMessage(msgID, maxWait);
            if (m == null) { return null; }
            if (m.Content is VMRequestReturn)
            {
                return (VMRequestReturn)m.Content;
            }
            else if (m.Content is ErrorMessage)
            {
                return null;
            }
            throw new NotImplementedException();
        }

        public string DeleteJob(string jobID)
        {
            AutomationMessage m = new AutomationMessage(new JobDeleteCommand(jobID));
            return Send(m);
        }

        public string CancelJob(string jobID)
        {
            AutomationMessage m = new AutomationMessage(new JobCancelCommand(jobID));
            return Send(m);
        }

        public string LockVM(string vmPath)
        {
            AutomationMessage m = new AutomationMessage(new LockVMCommand(vmPath));
            return Send(m);
        }

        public string UnLockVM(string vmPath)
        {
            AutomationMessage m = new AutomationMessage(new UnLockVMCommand(vmPath));
            return Send(m);
        }

        public string ReportJobStatus(JobCompleted jcm)
        {
            AutomationMessage m = new AutomationMessage(jcm);
            return Send(m);
        }

        public string RequestStatus(string hostName)
        {
            AutomationMessage m = new AutomationMessage(new SimpleRequest(SimpleRequests.JobReport));
            return Send(m);
        }

        public string RequestVMList(string hostName)
        {
            AutomationMessage m = new AutomationMessage(new SimpleRequest(SimpleRequests.AllVMRequest));
            return Send(m);
        }

        public JobReportReturn WaitForStatus(string msgID, TimeSpan maxWait)
        {
            AutomationMessage m = WaitForMessage(msgID, maxWait);
            if (m == null) { return null; }
            if (m.Content is JobReportReturn)
            {
                return m.Content as JobReportReturn;
            }
            return null;
        }
    }
}
