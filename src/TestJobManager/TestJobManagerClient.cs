using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using JobManagerClient;
using JobManagerInterfaces;
using System.IO;
using System.Configuration;
using System.Threading;

namespace TestJobManager
{
    [TestFixture]
    public class TestJobManagerClient
    {
        private JobClient sut;
        private MockSystem mock;
        private MockJobManagerService service;
        private string inboxPath;
        private string outboxPath;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            inboxPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "inbox");
            outboxPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "outbox");
            if (!Directory.Exists(inboxPath))
            {
                Directory.CreateDirectory(inboxPath);
            }
            if (!Directory.Exists(outboxPath))
            {
                Directory.CreateDirectory(outboxPath);
            }
            ConfigurationManager.AppSettings["inbox"] = inboxPath;
            ConfigurationManager.AppSettings["outbox"] = outboxPath;

            service = new MockJobManagerService(inboxPath, outboxPath);
            service.Start();
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
            service.Stop();

            //clear out log files
            Logger.Instance.Pause();
            DirectoryInfo workingDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            foreach (FileInfo logFile in workingDir.GetFiles("*.log"))
            {
                logFile.Delete();
            }
        }

        [SetUp]
        public void SetUp()
        {
            mock = new MockSystem();
            sut = new JobClient(mock);
        }

        [TearDown]
        public void TearDown()
        {
            sut.Stop();
            DirectoryInfo testDllDir = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestDll"));
            if (testDllDir.Exists)
            {
                try
                {
                    testDllDir.Delete(true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }

        [Test]
        public void TestRunEmptyJob()
        {
            JobStatus js = RunJob(null, null, null, null);

            Assert.That(js.Result.Success, Is.True);
            Assert.That(js.Result.SnapshotOnShutdown, Is.False);
            Assert.That(js.Result.Completed, Is.True);
            Assert.That(js.Result.Errors, Is.Empty);
            Assert.That(js.Result.CloneOnShutdown, Is.False);
            Assert.That(js.Result.Attachments, Is.Empty);
            Assert.That(js.Result.ExecutionResults, Is.Empty);
        }

        [Test]
        public void TestRunMockJobSuccess()
        {
            ExecutablePackage ep = new ExecutablePackage("mock.xml", null, "TestJobManager.dll", "TestJobManager.MockJobRunner", new SerializableDictionary<string, string>(), new SerializableDictionary<string, string>());
            List<ExecutablePackage> eps = new List<ExecutablePackage>();
            eps.Add(ep);

            SerializableDictionary<string, string> properties = new SerializableDictionary<string, string>();
            properties["Mock_ReportSuccess"] = "true";
            properties["Mock_StringToLog"] = "Mock Logged String";

            List<FileInfo> filesToCopy = new List<FileInfo>();
            filesToCopy.Add(new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestJobManager.dll")));
            JobStatus js = RunJob(filesToCopy, eps, properties, null);

            mock.VerifyEventOccured(MockEventType.UpdateStatus, "Loading external test DLL: TestJobManager.dll , TestJobManager.MockJobRunner", false);
            mock.VerifyEventOccured(MockEventType.UpdateStatus, "Executing Execute() method on external DLL", false);

            mock.VerifyEventOccured(MockEventType.UpdateStatus, properties["Mock_StringToLog"], false);

            Assert.That(js.Result.Success, Is.True);
            Assert.That(js.Result.SnapshotOnShutdown, Is.False);
            Assert.That(js.Result.Completed, Is.True);
            Assert.That(js.Result.Errors, Is.Empty);
            Assert.That(js.Result.CloneOnShutdown, Is.False);
            Assert.That(js.Result.Attachments, Is.Empty);
            Assert.That(js.Result.Logs, Is.Not.Empty);
            Assert.That(js.Result.ExecutionResults.Count, Is.EqualTo(1));

            VerifyLogContainsString(js.Result.Logs, properties["Mock_StringToLog"]);
        }

        [Test]
        public void TestRunMockJobSuccess2()
        {
            ExecutablePackage ep = new ExecutablePackage("mock.xml", null, "MockETP.dll", "MockETP.MockJobRunnerWithDependency", new SerializableDictionary<string, string>(), new SerializableDictionary<string, string>());
            List<ExecutablePackage> eps = new List<ExecutablePackage>();
            eps.Add(ep);

            SerializableDictionary<string, string> properties = new SerializableDictionary<string, string>();
            properties["Mock_ReportSuccess"] = "true";
            properties["Mock_StringToLog"] = "Mock Logged String";

            List<FileInfo> filesToCopy = new List<FileInfo>();
            DirectoryInfo mockETPDir = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\MockETP\bin\Debug"));
            filesToCopy.AddRange(mockETPDir.GetFiles());
            //filesToCopy.Add(new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\MockETP\bin\Debug\MockETP.dll")));
            JobStatus js = RunJob(filesToCopy, eps, properties, null);

            mock.VerifyEventOccured(MockEventType.UpdateStatus, "Loading external test DLL: MockETP.dll , MockETP.MockJobRunnerWithDependency", false);
            mock.VerifyEventOccured(MockEventType.UpdateStatus, "Executing Execute() method on external DLL", false);

            mock.VerifyEventOccured(MockEventType.UpdateStatus, properties["Mock_StringToLog"], false);

            Assert.That(js.Result.Success, Is.True);
            Assert.That(js.Result.SnapshotOnShutdown, Is.False);
            Assert.That(js.Result.Completed, Is.True);
            Assert.That(js.Result.Errors, Is.Empty);
            Assert.That(js.Result.CloneOnShutdown, Is.False);
            Assert.That(js.Result.Attachments, Is.Empty);
            Assert.That(js.Result.Logs, Is.Not.Empty);
            Assert.That(js.Result.ExecutionResults.Count, Is.EqualTo(1));

            VerifyLogContainsString(js.Result.Logs, properties["Mock_StringToLog"]);
        }

        [Test]
        public void JobFailMissingDLL()
        {
            ExecutablePackage ep = new ExecutablePackage("mock.xml", null, "TestJobManager.dll", "TestJobManager.MockJobRunner", new SerializableDictionary<string, string>(), new SerializableDictionary<string, string>());
            List<ExecutablePackage> eps = new List<ExecutablePackage>();
            eps.Add(ep);

            JobStatus js = RunJob(null, eps, null, null);

            mock.VerifyEventOccured(MockEventType.UpdateStatus, "System.IO.FileNotFoundException: Could not load file", true);

            Assert.That(js.Result.Completed, Is.False);
            Assert.That(js.Result.Success, Is.False);
            Assert.That(js.Result.ExecutionResults.Count, Is.EqualTo(1));
            Assert.That(js.Result.Errors, Is.Not.Empty);

            VerifyStringInList(js.Result.Errors, "System.IO.FileNotFoundException: Could not load file");
        }

        [Test]
        public void JobFailWrongClassName()
        {
            ExecutablePackage ep = new ExecutablePackage("mock.xml", null, "TestJobManager.dll", "TestJobManager.InvalidName", new SerializableDictionary<string, string>(), new SerializableDictionary<string, string>());
            List<ExecutablePackage> eps = new List<ExecutablePackage>();
            eps.Add(ep);

            List<FileInfo> filesToCopy = new List<FileInfo>();
            filesToCopy.Add(new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestJobManager.dll")));
            JobStatus js = RunJob(filesToCopy, eps, null, null);

            mock.VerifyEventOccured(MockEventType.UpdateStatus, "System.Exception: method/class not found", true);

            Assert.That(js.Result.Completed, Is.False);
            Assert.That(js.Result.Success, Is.False);
            Assert.That(js.Result.ExecutionResults.Count, Is.EqualTo(1));
            Assert.That(js.Result.Errors, Is.Not.Empty);

            VerifyStringInList(js.Result.Errors, "System.Exception: method/class not found");
        }

        [Test]
        public void JobFailPackageThrowsException()
        {
            ExecutablePackage ep = new ExecutablePackage("mock.xml", null, "TestJobManager.dll", "TestJobManager.MockJobRunner", new SerializableDictionary<string, string>(), new SerializableDictionary<string, string>());
            List<ExecutablePackage> eps = new List<ExecutablePackage>();
            eps.Add(ep);

            SerializableDictionary<string, string> properties = new SerializableDictionary<string, string>();
            properties["Mock_ReportSuccess"] = "true";
            properties["Mock_StringToLog"] = "Mock Logged String";
            properties["Mock_ThrowException"] = "Expected Exception";

            List<FileInfo> filesToCopy = new List<FileInfo>();
            filesToCopy.Add(new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestJobManager.dll")));
            JobStatus js = RunJob(filesToCopy, eps, properties, null);

            mock.VerifyEventOccured(MockEventType.UpdateStatus, "Loading external test DLL: TestJobManager.dll , TestJobManager.MockJobRunner", false);
            mock.VerifyEventOccured(MockEventType.UpdateStatus, "Executing Execute() method on external DLL", false);
            mock.VerifyEventOccured(MockEventType.UpdateStatus, properties["Mock_ThrowException"], true);
            mock.VerifyEventOccured(MockEventType.UpdateStatus, properties["Mock_StringToLog"], false);

            Assert.That(js.Result.Completed, Is.False);
            Assert.That(js.Result.Success, Is.False);
            Assert.That(js.Result.ExecutionResults.Count, Is.EqualTo(1));
            Assert.That(js.Result.Errors, Is.Not.Empty);

            VerifyStringInList(js.Result.Errors, "System.Exception: " + properties["Mock_ThrowException"]);

            VerifyLogContainsString(js.Result.Logs, properties["Mock_StringToLog"]);
            VerifyLogContainsString(js.Result.Logs, properties["Mock_ThrowException"]);
        }

        [Test]
        public void JobFailISOCopyFail()
        {
            string expectedException = "System.IO.FileNotFoundException: Could not find file";

            SerializableDictionary<string, string> isos = new SerializableDictionary<string, string>();
            isos["Main"] = "nonexistant.iso";
            ExecutablePackage ep = new ExecutablePackage("mock.xml", null, "TestJobManager.dll", "TestJobManager.MockJobRunner", new SerializableDictionary<string, string>(), new SerializableDictionary<string, string>());
            List<ExecutablePackage> eps = new List<ExecutablePackage>();
            eps.Add(ep);

            List<FileInfo> filesToCopy = new List<FileInfo>();
            filesToCopy.Add(new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestJobManager.dll")));
            JobStatus js = RunJob(filesToCopy, eps, null, isos);

            mock.VerifyEventOccured(MockEventType.UpdateStatus, expectedException, true);

            Assert.That(js.Result.Completed, Is.False);
            Assert.That(js.Result.Success, Is.False);
            Assert.That(js.Result.ExecutionResults.Count, Is.EqualTo(1));
            Assert.That(js.Result.Errors, Is.Not.Empty);

            VerifyStringInList(js.Result.Errors, expectedException);

            VerifyLogContainsString(js.Result.Logs, expectedException);
        }

        [Test]
        public void JobFailTestDLLDirectoryLocked()
        {
            DirectoryInfo testdllDir = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestDLL"));
            testdllDir.Create();
            FileInfo tempFile = new FileInfo(Path.Combine(testdllDir.FullName, "temp.txt"));
            FileStream fs = new FileStream(tempFile.FullName, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            try
            {
                string expectedException = "System.IO.IOException: The process cannot access the file 'temp.txt' because it is being used by another process.";

                ExecutablePackage ep = new ExecutablePackage("mock.xml", null, "TestJobManager.dll", "TestJobManager.MockJobRunner", new SerializableDictionary<string, string>(), new SerializableDictionary<string, string>());
                List<ExecutablePackage> eps = new List<ExecutablePackage>();
                eps.Add(ep);

                List<FileInfo> filesToCopy = new List<FileInfo>();
                filesToCopy.Add(new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestJobManager.dll")));
                JobStatus js = RunJob(filesToCopy, eps, null, null);

                mock.VerifyEventOccured(MockEventType.UpdateStatus, expectedException, true);

                Assert.That(js.Result.Completed, Is.False);
                Assert.That(js.Result.Success, Is.False);
                Assert.That(js.Result.ExecutionResults.Count, Is.EqualTo(1));
                Assert.That(js.Result.Errors, Is.Not.Empty);

                VerifyStringInList(js.Result.Errors, expectedException);

                VerifyLogContainsString(js.Result.Logs, expectedException);
            }
            finally
            {
                fs.Close();
                tempFile.Delete();
                testdllDir.Delete(true);
            }
        }

        #region Utility
        private JobStatus RunJob(IEnumerable<FileInfo> filesToCopy, List<ExecutablePackage> packages, SerializableDictionary<string, string> jobProperties, SerializableDictionary<string, string> isos)
        {
            if (filesToCopy == null)
            {
                filesToCopy = new List<FileInfo>();
            }
            if (packages == null)
            {
                packages = new List<ExecutablePackage>();
            }
            if (jobProperties == null)
            {
                jobProperties = new SerializableDictionary<string, string>();
            }
            if (isos == null)
            {
                isos = new SerializableDictionary<string, string>();
            }
            DirectoryInfo tempDir = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tempdll"));
            tempDir.Create();
            try
            {
                foreach (FileInfo file in filesToCopy)
                {
                    File.Copy(file.FullName, Path.Combine(tempDir.FullName, file.Name));
                }
                foreach (ExecutablePackage ep in packages)
                {
                    ep.ContentDirectory = tempDir.FullName;
                }
                Job j = new Job("test build path", "TestConfig", isos, packages, jobProperties);
                service.AddJob(j, JobStates.VMStarted);
                sut.Start();
                sut.WaitForExit();

                foreach (MockEvent evnt in mock.Events)
                {
                    System.Diagnostics.Debug.WriteLine(evnt.Time.ToString() + " " + evnt.EventType.ToString() + " " + evnt.Payload);
                }

                //wait for service to finish processing
                Thread.Sleep(1000);

                mock.VerifyEventOccured(MockEventType.UpdateStatus, "Requesting Jobs from Job Manager", false);
                mock.VerifyEventOccured(MockEventType.UpdateStatus, "Sent request with message id: ", true);
                mock.VerifyEventOccured(MockEventType.UpdateStatus, "Waiting for Job response from Job Manager", false);
                mock.VerifyEventOccured(MockEventType.UpdateStatus, "Found Job: " + j.JobID, false);
                //for (int i = 0; i < packages.Count; i++)
                //{
                //    mock.VerifyEventOccured(MockEventType.UpdateStatus, "Copying data from \"" + tempDir.FullName + "\" to \"" + Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestDLL\\" + i) + "\"", false);
                //}
                mock.VerifyEventOccured(MockEventType.UpdateStatus, "Sending Job Result", false);
                mock.VerifyEventOccured(MockEventType.UpdateStatus, "Job Result Sent", false);

                JobStatus js = service.GetJobStatus(j.JobID);
                Assert.That(js, Is.Not.Null);
                Assert.That(js.State, Is.EqualTo(JobStates.AutoFinished));
                Assert.That(js.Result, Is.Not.Null);
                Assert.That(js.Result.Logs, Is.Not.Empty);
                VerifyLogContainsString(js.Result.Logs, "Found Job: " + j.JobID);

                return js;
            }
            finally
            {
                tempDir.Delete(true);
            }
        }

        private void VerifyLogContainsString(List<FileData> logs, string expected)
        {
            bool found = false;
            List<string> searchedLogFileNames = new List<string>();
            foreach (FileData logFile in logs)
            {
                searchedLogFileNames.Add(logFile.Name);
                using (TextReader tr = new StreamReader(new MemoryStream(logFile.Data)))
                {
                    string line = null;
                    while ((line = tr.ReadLine()) != null)
                    {
                        if (line.Contains(expected))
                        {
                            found = true;
                            break;
                        }
                    }
                }
                if (found)
                {
                    break;
                }
            }
            Assert.True(found, "Could not find string \"{0}\" in logs {1}", expected, string.Join(",", searchedLogFileNames.ToArray())); 
        }

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
        #endregion
    }
}
