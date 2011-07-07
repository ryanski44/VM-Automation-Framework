using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using System.IO;
using System.Configuration;
using System.Diagnostics;
using JobManagerInterfaces;

namespace TestJobManager
{
    [TestFixture]
    public class TestJobManagerService
    {
        private string inboxPath;
        private string outboxPath;
        private string dropPath;
        private JobManagerServiceWrapper sut;
        private Dictionary<string, MockVMConnection> vmHash;
        private DateTime testStart;

        private static TimeSpan DEFAULT_WAIT = TimeSpan.FromMinutes(1);

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            inboxPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "inbox");
            outboxPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "outbox");
            dropPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "drop");
            if (!Directory.Exists(inboxPath))
            {
                Directory.CreateDirectory(inboxPath);
            }
            if (!Directory.Exists(outboxPath))
            {
                Directory.CreateDirectory(outboxPath);
            }
            if (!Directory.Exists(dropPath))
            {
                Directory.CreateDirectory(dropPath);
            }
            ConfigurationManager.AppSettings["inbox"] = inboxPath;
            ConfigurationManager.AppSettings["outbox"] = outboxPath;
            ConfigurationManager.AppSettings["filedrop"] = dropPath;
            ConfigurationManager.AppSettings["maxvms"] = "3";
        }

        [TestFixtureTearDown]
        public void TestFixtureTearDown()
        {
            if (Directory.Exists(inboxPath))
            {
                Directory.Delete(inboxPath, true);
            }
            if (Directory.Exists(outboxPath))
            {
                Directory.Delete(outboxPath, true);
            }
            if (Directory.Exists(dropPath))
            {
                Directory.Delete(dropPath, true);
            }
        }

        [SetUp]
        public void SetUp()
        {
            testStart = DateTime.Now;

            vmHash = new Dictionary<string, MockVMConnection>();
            List<string[]> mockVMs = new List<string[]>();
            mockVMs.Add(new string[] { "MockVMConfig1", "", "Snapshot1", "MockVMName1" });
            mockVMs.Add(new string[] { "MockVMConfig2", "", "Snapshot2", "MockVMName2" });
            sut = new JobManagerServiceWrapper();
            foreach (string[] mockInfo in mockVMs)
            {
                MockVMConnection vmConn = sut.MockVMHost.AddMockVM(mockInfo[3]);
                vmHash[vmConn._VMName] = vmConn;
                mockInfo[1] = vmConn._Identifier;
            }
            using (TextWriter tw = new StreamWriter(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"vm_list.txt"),false))
            {
                foreach (string[] mockInfo in mockVMs)
                {
                    tw.WriteLine(string.Join(", ", mockInfo));
                }
            }
            
        }

        [TearDown]
        public void TearDown()
        {
            string vmListFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"vm_list.txt");
            if (File.Exists(vmListFilePath))
            {
                File.Delete(vmListFilePath);
            }

            DirectoryInfo jobsDir = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "jobs"));
            if (jobsDir.Exists)
            {
                jobsDir.Delete(true);
            }
        }

        [Test]
        public void OnStartVMsChecked()
        {
            sut.EmulateServiceStart(null);
            sut.EmulateServiceStop();
            VerifyMockVMActionInvoked(vmHash["MockVMName1"], VMActionType.GetHasValidConnection);
            VerifyMockVMActionInvoked(vmHash["MockVMName2"], VMActionType.GetHasValidConnection);
            VerifyAppLogContainsString("JobManagerService started successfully.", EventLogEntryType.Information, testStart);
            VerifyAppLogDoesNotContain(EventLogEntryType.Warning, testStart);
            VerifyAppLogDoesNotContain(EventLogEntryType.Error, testStart);
        }

        [Test]
        public void OnStartInvalidVMChecked()
        {
            vmHash["MockVMName2"].HasValidConnection = false;
            sut.EmulateServiceStart(null);
            sut.EmulateServiceStop();
            VerifyAppLogContainsString("Could not find VM \"" + vmHash["MockVMName2"]._Identifier + "\"", EventLogEntryType.Warning, testStart);
            VerifyAppLogDoesNotContain(EventLogEntryType.Error, testStart);
        }

        [Test]
        public void RunDependentJob()
        {
            sut.EmulateServiceStart(null);
            DirectoryInfo tempDir = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tempdll"));
            tempDir.Create();
            try
            {
                List<ExecutablePackage> packages = new List<ExecutablePackage>();
                ExecutablePackage package = new ExecutablePackage("mock.xml", tempDir.FullName, "TestJobManager.dll", "TestJobManager.MockJobRunner", new SerializableDictionary<string,string>(), new SerializableDictionary<string,string>());
                packages.Add(package);
                Job j1 = new Job("nobuildpath", "MockVMConfig1", new SerializableDictionary<string,string>(), packages, new SerializableDictionary<string,string>());
                Job j2 = new Job("nobuildpath", "MockVMConfig2", new SerializableDictionary<string, string>(), packages, new SerializableDictionary<string, string>());
                j2.DependsOnJobIds.Add(j1.JobID);
                MessageSendRecieve msr = new MessageSendRecieve(new DirectoryInfo(inboxPath), new DirectoryInfo(outboxPath));
                DateTime queuedJobsDateTime = DateTime.Now;
                string job2msgID = msr.QueueJob(j2);
                string job1msgID = msr.QueueJob(j1);
                //wait for job 1 to start
                Assert.True(WaitForVMAction(vmHash["MockVMName1"], VMActionType.Start, queuedJobsDateTime, TimeSpan.FromSeconds(10)));
                //send request for job 1
                AutomationMessage m = new AutomationMessage(new SimpleRequest(SimpleRequests.JobRequest));
                m.From = "MockVMName1";
                Job j = msr.WaitForJob(msr.Send(m), DEFAULT_WAIT);
                Assert.That(j, Is.Not.Null);
                //send finished for job 1
                DateTime finishedSentDateTime = DateTime.Now;
                msr.ReportJobStatus(new JobCompleted(j1, new JobResult()));
                //wait for job 2 to start
                Assert.True(WaitForVMAction(vmHash["MockVMName2"], VMActionType.Start, finishedSentDateTime, TimeSpan.FromSeconds(10)));
                //send request for job 2
                m = new AutomationMessage(new SimpleRequest(SimpleRequests.JobRequest));
                m.From = "MockVMName2";
                j = msr.WaitForJob(msr.Send(m), DEFAULT_WAIT);
                Assert.That(j, Is.Not.Null);
                //send finished for job2
                msr.ReportJobStatus(new JobCompleted(j2, new JobResult()));

                Assert.That(msr.WaitForJobCompletion(job1msgID, DEFAULT_WAIT), Is.Not.Null);
                Assert.That(msr.WaitForJobCompletion(job2msgID, DEFAULT_WAIT), Is.Not.Null);

                sut.EmulateServiceStop();

                VerifyAppLogDoesNotContain(EventLogEntryType.Warning, testStart);
                VerifyAppLogDoesNotContain(EventLogEntryType.Error, testStart);

                VerifyMockVMActionInvoked(vmHash["MockVMName1"], VMActionType.RevertToNamedSnapshot, "Snapshot1");
                VerifyMockVMActionInvoked(vmHash["MockVMName2"], VMActionType.RevertToNamedSnapshot, "Snapshot2");
            }
            finally
            {
                tempDir.Delete(true);
            }
        }

        [Test]
        public void EnsureJobsQueuedAfterMaxVMsRunningReached()
        {
            sut.EmulateServiceStart(null);
            DirectoryInfo tempDir = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tempdll"));
            tempDir.Create();
            string existingMaxVMSetting = ConfigurationManager.AppSettings["maxvms"];
            ConfigurationManager.AppSettings["maxvms"] = "1";
            try
            {
                List<ExecutablePackage> packages = new List<ExecutablePackage>();
                ExecutablePackage package = new ExecutablePackage("mock.xml", tempDir.FullName, "TestJobManager.dll", "TestJobManager.MockJobRunner", new SerializableDictionary<string, string>(), new SerializableDictionary<string, string>());
                packages.Add(package);
                Job j1 = new Job("nobuildpath", "MockVMConfig1", new SerializableDictionary<string, string>(), packages, new SerializableDictionary<string, string>());
                Job j2 = new Job("nobuildpath", "MockVMConfig2", new SerializableDictionary<string, string>(), packages, new SerializableDictionary<string, string>());
                MessageSendRecieve msr = new MessageSendRecieve(new DirectoryInfo(inboxPath), new DirectoryInfo(outboxPath));
                DateTime queuedJobsDateTime = DateTime.Now;
                string job2msgID = msr.QueueJob(j1);
                string job1msgID = msr.QueueJob(j2);
                //wait for job 1 to start
                Assert.True(WaitForVMAction(vmHash["MockVMName1"], VMActionType.Start, queuedJobsDateTime, TimeSpan.FromSeconds(5)));
                //make sure job 2 doesn't start/hasn't started
                Assert.False(WaitForVMAction(vmHash["MockVMName2"], VMActionType.Start, queuedJobsDateTime, TimeSpan.FromSeconds(1)));
                
                //send request for job 1
                AutomationMessage m = new AutomationMessage(new SimpleRequest(SimpleRequests.JobRequest));
                m.From = "MockVMName1";
                Job j = msr.WaitForJob(msr.Send(m), DEFAULT_WAIT);
                Assert.That(j, Is.Not.Null);
                //send finished for job 1
                DateTime finishedSentDateTime = DateTime.Now;
                msr.ReportJobStatus(new JobCompleted(j1, new JobResult()));

                //wait for job 2 to start
                Assert.True(WaitForVMAction(vmHash["MockVMName2"], VMActionType.Start, finishedSentDateTime, TimeSpan.FromSeconds(5)));
                //send request for job 2
                m = new AutomationMessage(new SimpleRequest(SimpleRequests.JobRequest));
                m.From = "MockVMName2";
                j = msr.WaitForJob(msr.Send(m), DEFAULT_WAIT);
                Assert.That(j, Is.Not.Null);
                //send finished for job2
                msr.ReportJobStatus(new JobCompleted(j2, new JobResult()));

                Assert.That(msr.WaitForJobCompletion(job1msgID, DEFAULT_WAIT), Is.Not.Null);
                Assert.That(msr.WaitForJobCompletion(job2msgID, DEFAULT_WAIT), Is.Not.Null);

                sut.EmulateServiceStop();

                VerifyAppLogDoesNotContain(EventLogEntryType.Warning, testStart);
                VerifyAppLogDoesNotContain(EventLogEntryType.Error, testStart);

                VerifyMockVMActionInvoked(vmHash["MockVMName1"], VMActionType.RevertToNamedSnapshot, "Snapshot1");
                VerifyMockVMActionInvoked(vmHash["MockVMName2"], VMActionType.RevertToNamedSnapshot, "Snapshot2");
            }
            finally
            {
                tempDir.Delete(true);
                ConfigurationManager.AppSettings["maxvms"] = existingMaxVMSetting;
            }
        }

        [Test]
        public void TestRunNonExistantVMConfig()
        {
            sut.EmulateServiceStart(null);
            List<ExecutablePackage> packages = new List<ExecutablePackage>();
            Job j1 = new Job("nobuildpath", "NonExistantVMConfig", new SerializableDictionary<string, string>(), null, new SerializableDictionary<string, string>());
            MessageSendRecieve msr = new MessageSendRecieve(new DirectoryInfo(inboxPath), new DirectoryInfo(outboxPath));
            DateTime queuedJobsDateTime = DateTime.Now;
            string job1msgID = msr.QueueJob(j1);

            //wait for job completion
            JobCompleted jobCompleted = msr.WaitForJobCompletion(job1msgID, DEFAULT_WAIT);

            sut.EmulateServiceStop();

            Assert.That(jobCompleted, Is.Not.Null);
            Assert.That(jobCompleted.Job, Is.Not.Null);
            Assert.That(jobCompleted.Result, Is.Not.Null);
            Assert.That(jobCompleted.Result.Errors, Is.Not.Empty);
            VerifyStringInList(jobCompleted.Result.Errors, "Could not find a VM suitable for this configuration(NonExistantVMConfig)");

            VerifyAppLogDoesNotContain(EventLogEntryType.Warning, testStart);
            VerifyAppLogDoesNotContain(EventLogEntryType.Error, testStart);
        }

        [Test]
        public void TestNullDirPathInJob()
        {
            sut.EmulateServiceStart(null);
            List<ExecutablePackage> packages = new List<ExecutablePackage>();
            ExecutablePackage package = new ExecutablePackage("mock.xml", null, "TestJobManager.dll", "TestJobManager.MockJobRunner", new SerializableDictionary<string, string>(), new SerializableDictionary<string, string>());
            packages.Add(package);
            Job j1 = new Job("nobuildpath", "MockVMConfig1", new SerializableDictionary<string, string>(), packages, new SerializableDictionary<string, string>());
            MessageSendRecieve msr = new MessageSendRecieve(new DirectoryInfo(inboxPath), new DirectoryInfo(outboxPath));
            DateTime queuedJobsDateTime = DateTime.Now;
            string job1msgID = msr.QueueJob(j1);

            //wait for job completion
            JobCompleted jobCompleted = msr.WaitForJobCompletion(job1msgID, DEFAULT_WAIT);

            sut.EmulateServiceStop();

            Assert.That(jobCompleted, Is.Not.Null);
            Assert.That(jobCompleted.Job, Is.Not.Null);
            Assert.That(jobCompleted.Result, Is.Not.Null);
            Assert.That(jobCompleted.Result.Errors, Is.Not.Empty);
            VerifyStringInList(jobCompleted.Result.Errors, "Exception: Value cannot be null.\nParameter name: path");

            VerifyAppLogDoesNotContain(EventLogEntryType.Warning, testStart);
            VerifyAppLogDoesNotContain(EventLogEntryType.Error, testStart);
        }

        [Test]
        public void TestNullPackageList()
        {
            sut.EmulateServiceStart(null);
            Job j1 = new Job("nobuildpath", "MockVMConfig1", new SerializableDictionary<string, string>(), null, new SerializableDictionary<string, string>());
            MessageSendRecieve msr = new MessageSendRecieve(new DirectoryInfo(inboxPath), new DirectoryInfo(outboxPath));
            DateTime queuedJobsDateTime = DateTime.Now;
            string job1msgID = msr.QueueJob(j1);

            //wait for job completion
            JobCompleted jobCompleted = msr.WaitForJobCompletion(job1msgID, DEFAULT_WAIT);

            sut.EmulateServiceStop();

            VerifyAppLogDoesNotContain(EventLogEntryType.Warning, testStart);
            VerifyAppLogDoesNotContain(EventLogEntryType.Error, testStart);

            Assert.That(jobCompleted, Is.Not.Null);
            Assert.That(jobCompleted.Job, Is.Not.Null);
            Assert.That(jobCompleted.Result, Is.Not.Null);
            Assert.That(jobCompleted.Result.Errors, Is.Not.Empty);
            VerifyStringInList(jobCompleted.Result.Errors, "Job does not have any packages defined"); 
        }

        [Test]
        public void TestNullConfiguration()
        {
            sut.EmulateServiceStart(null);
            Job j1 = new Job("abc", null, new SerializableDictionary<string, string>(), new List<ExecutablePackage>(), new SerializableDictionary<string, string>());
            MessageSendRecieve msr = new MessageSendRecieve(new DirectoryInfo(inboxPath), new DirectoryInfo(outboxPath));
            DateTime queuedJobsDateTime = DateTime.Now;
            string job1msgID = msr.QueueJob(j1);

            //wait for job completion
            JobCompleted jobCompleted = msr.WaitForJobCompletion(job1msgID, DEFAULT_WAIT);

            sut.EmulateServiceStop();

            VerifyAppLogDoesNotContain(EventLogEntryType.Warning, testStart);
            VerifyAppLogDoesNotContain(EventLogEntryType.Error, testStart);

            Assert.That(jobCompleted, Is.Not.Null);
            Assert.That(jobCompleted.Job, Is.Not.Null);
            Assert.That(jobCompleted.Result, Is.Not.Null);
            Assert.That(jobCompleted.Result.Errors, Is.Not.Empty);
            VerifyStringInList(jobCompleted.Result.Errors, "Configuration cannot be null or empty");
        }

        [Test]
        public void TestNullISOs()
        {
            sut.EmulateServiceStart(null);
            DirectoryInfo tempDir = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tempdll"));
            tempDir.Create();
            try
            {
                List<ExecutablePackage> packages = new List<ExecutablePackage>();
                ExecutablePackage package = new ExecutablePackage("mock.xml", tempDir.FullName, "TestJobManager.dll", "TestJobManager.MockJobRunner", new SerializableDictionary<string, string>(), new SerializableDictionary<string, string>());
                packages.Add(package);

                Job j1 = new Job(null, "MockVMConfig1", null, packages, new SerializableDictionary<string, string>());
                MessageSendRecieve msr = new MessageSendRecieve(new DirectoryInfo(inboxPath), new DirectoryInfo(outboxPath));
                DateTime queuedJobsDateTime = DateTime.Now;
                string job1msgID = msr.QueueJob(j1);

                //wait for job 1 to start
                Assert.True(WaitForVMAction(vmHash["MockVMName1"], VMActionType.Start, queuedJobsDateTime, TimeSpan.FromSeconds(5)));
                
                //send request for job 1
                AutomationMessage m = new AutomationMessage(new SimpleRequest(SimpleRequests.JobRequest));
                m.From = "MockVMName1";
                Job j = msr.WaitForJob(msr.Send(m), DEFAULT_WAIT);
                Assert.That(j, Is.Not.Null);
                Assert.That(j.JobID, Is.EqualTo(j1.JobID));

                //send finished for job 1
                DateTime finishedSentDateTime = DateTime.Now;
                JobResult jr = new JobResult();
                jr.Completed = true;
                ExecutionResult er = new ExecutionResult();
                er.Success = true;
                jr.ExecutionResults.Add(er);
                msr.ReportJobStatus(new JobCompleted(j1, jr));

                //wait for job completion
                JobCompleted jobCompleted = msr.WaitForJobCompletion(job1msgID, DEFAULT_WAIT);

                sut.EmulateServiceStop();

                VerifyAppLogDoesNotContain(EventLogEntryType.Warning, testStart);
                VerifyAppLogDoesNotContain(EventLogEntryType.Error, testStart);

                Assert.That(jobCompleted, Is.Not.Null);
                Assert.That(jobCompleted.Job, Is.Not.Null);
                Assert.That(jobCompleted.Result, Is.Not.Null);
                Assert.That(jobCompleted.Result.Errors, Is.Empty);
                Assert.That(jobCompleted.Result.Success, Is.True);
                Assert.That(jobCompleted.Result.Completed, Is.True);
            }
            finally
            {
                tempDir.Delete(true);
            }
        }

        #region Utility

        private void VerifyStringInList(IEnumerable<string> list, string expected)
        {
            bool found = false;
            foreach (string str in list)
            {
                if (str.Contains(expected))
                {
                    found = true;
                    break;
                }
            }
            Assert.True(found, "Could not find string \"{0}\"", expected);
        }

        private bool WaitForVMAction(MockVMConnection vm, VMActionType actionType, DateTime minimumTime, TimeSpan timeout)
        {
            DateTime start = DateTime.Now;
            while (DateTime.Now - start < timeout)
            {
                foreach (VMAction action in vm.History)
                {
                    if (action.Time >= minimumTime && action.Action == actionType)
                    {
                        return true;
                    }
                }
                System.Threading.Thread.Sleep(100);
            }
            return false;
        }

        private void VerifyMockVMActionInvoked(MockVMConnection vm, VMActionType actionType)
        {
            VerifyMockVMActionInvoked(vm, actionType, new string[0]);
        }

        private void VerifyMockVMActionInvoked(MockVMConnection vm, VMActionType actionType, params string[] parameters)
        {
            bool found = false;
            foreach (VMAction action in vm.History)
            {
                if (action.Action == actionType)
                {
                    bool parametersDifferent = false;
                    if (parameters.Length == action.Params.Length)
                    {
                        for (int i = 0; i < parameters.Length; i++)
                        {
                            if (parameters[i] != action.Params[i])
                            {
                                parametersDifferent = true;
                            }
                        }
                    }
                    else
                    {
                        parametersDifferent = true;
                    }
                    if (!parametersDifferent)
                    {
                        found = true;
                    }
                }
            }
            Assert.True(found, "Could not find action \"{0}\" with params \"{1}\" for VM \"{2}\"", actionType.ToString(), string.Join(", ", parameters), vm._VMName);
        }

        private void VerifyAppLogContainsString(string expectedMessageSubString, EventLogEntryType expectedLevel, DateTime minimumTime)
        {
            minimumTime = minimumTime.AddMilliseconds((minimumTime.Millisecond + 1) * -1);
            EventLog appLog = new EventLog("Application");

            bool found = false;
            foreach (EventLogEntry entry in appLog.Entries)
            {
                if (entry.TimeGenerated >= minimumTime &&
                    entry.Source == "VM Job Manager Service" &&
                    entry.EntryType == expectedLevel &&
                    entry.Message.Contains(expectedMessageSubString))
                {
                    found = true;
                    break;
                }
            }
            Assert.True(found, "Could not find event log entry with message \"{0}\" and severity \"{1}\" in application log", expectedMessageSubString, expectedLevel.ToString());
        }

        private void VerifyAppLogDoesNotContain(EventLogEntryType unExpectedLevel, DateTime minimumTime)
        {
            minimumTime = minimumTime.AddMilliseconds((minimumTime.Millisecond + 1) * -1);
            EventLog appLog = new EventLog("Application");

            bool found = false;
            string message = "";
            foreach (EventLogEntry entry in appLog.Entries)
            {
                if (entry.TimeGenerated >= minimumTime &&
                    entry.Source == "VM Job Manager Service" &&
                    entry.EntryType == unExpectedLevel)
                {
                    found = true;
                    message = entry.Message;
                    break;
                }
            }
            Assert.False(found, "Found unexpected event log entry \"{0}\" with severity \"{1}\" in application log", message, unExpectedLevel.ToString());
        }
        #endregion
    }
}
