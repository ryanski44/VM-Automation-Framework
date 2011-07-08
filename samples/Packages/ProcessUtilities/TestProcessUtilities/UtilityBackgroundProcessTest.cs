using ProcessUtilities;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using NUnit.Framework;

namespace TestProcessUtilities
{
    
    
    /// <summary>
    ///This is a test class for UtilityBackgroundProcessTest and is intended
    ///to contain all UtilityBackgroundProcessTest Unit Tests
    ///</summary>
    [TestFixture]
    public class UtilityBackgroundProcessTest
    {
        private static string WORKING_DIR = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        private UtilityBackgroundProcess sut;

        [SetUp]
        public void MyTestInitialize()
        {
            sut = new UtilityBackgroundProcess(Path.Combine(WORKING_DIR, "MockConsoleApp.exe"));
        }
        
        [TearDown]
        public void MyTestCleanup()
        {
            sut = null;
        }


        /// <summary>
        ///A test for UtilityBackgroundProcess Constructor
        ///</summary>
        [Test]
        public void UtilityBackgroundProcessConstructorTest()
        {
            string program = Path.Combine(WORKING_DIR, "MockConsoleApp.exe");
            UtilityBackgroundProcess target = new UtilityBackgroundProcess(program);
            Assert.AreEqual(program, target.ProgramEXE);
        }

        /// <summary>
        ///A test for Run
        ///</summary>
        [Test]
        public void RunTestNoArgsSuccess()
        {
            string arguments = string.Empty; 
            bool expected = true; 
            bool actual = sut.Run(arguments);
            Assert.AreEqual(expected, actual);
        }

        /// <summary>
        ///A test for Run
        ///</summary>
        [Test]
        public void RunTestArgsSuccess()
        {
            string arguments = "2 2"; 
            bool expected = true; 
            bool actual;
            actual = sut.Run(arguments);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void RunTestFailNull()
        {
            string program = null; 
            UtilityBackgroundProcess target = new UtilityBackgroundProcess(program);

            string arguments = String.Empty; 
            bool expected = false;
            bool actual = target.Run(arguments);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void RunTestFailEmpty()
        {
            string program = String.Empty;
            UtilityBackgroundProcess target = new UtilityBackgroundProcess(program);

            string arguments = String.Empty;
            bool expected = false;
            bool actual = target.Run(arguments);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void RunTestFailInvalid()
        {
            string program = "doesnotexist.exe";
            UtilityBackgroundProcess target = new UtilityBackgroundProcess(program);

            string arguments = String.Empty;
            bool expected = false;
            bool actual = target.Run(arguments);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void RunTestFailAlreadyStarted()
        {
            //start the process
            sut.Run("0 4");
            //try to start again, expect exception
            sut.Run(String.Empty);
        }

        /// <summary>
        ///A test for Run
        ///</summary>
        [Test]
        public void RunTestWorkingDir()
        {
            string arguments = string.Empty;
            DirectoryInfo di = new DirectoryInfo(Path.Combine(WORKING_DIR, "abc"));
            di.Create();
            try
            {
                string workingDir = di.FullName;
                bool expected = true;
                bool actual = sut.Run(arguments, workingDir);
                Assert.AreEqual(expected, actual);
                sut.WaitForExit();
                Assert.AreEqual(workingDir, sut.proc.StartInfo.WorkingDirectory);
            }
            finally
            {
                di.Delete();
            }
        }

        /// <summary>
        ///A test for Stop
        ///</summary>
        [Test]
        public void StopTest()
        {
            sut.Run("0 30");
            Thread.Sleep(500);
            sut.Stop();
            Assert.IsTrue(sut.WaitForExit(TimeSpan.FromSeconds(1)));
            Assert.IsTrue(sut.IsStopped);
            Assert.AreEqual(-1, sut.ExitCode);
        }

        [Test]
        public void WaitForExitTest()
        {
            int secondsToRun = 5;
            int secondsTooLong = 10;
            DateTime start = DateTime.Now;
            sut.Run("0 " + secondsToRun);
            bool expected = true;
            bool actual;
            actual = sut.WaitForExit();
            DateTime end = DateTime.Now;
            Assert.AreEqual(expected, actual);
            Assert.IsTrue(end.Subtract(start).TotalSeconds < secondsTooLong);
        }

        [Test]
        public void WaitForExitTestFalse()
        {
            int secondsToRun = 10;
            int secondsToWait = 3;
            DateTime start = DateTime.Now;
            sut.Run("0 " + secondsToRun);
            bool expected = false;
            bool actual;
            actual = sut.WaitForExit(TimeSpan.FromSeconds(secondsToWait));
            DateTime end = DateTime.Now;
            Assert.AreEqual(expected, actual);
            Assert.IsTrue(end.Subtract(start).TotalSeconds < secondsToRun);
        }

        [Test]
        public void WaitForExitTrueGetOutput()
        {
            int secondsToRun = 5;
            sut.Run("0 " + secondsToRun);
            bool expected = true;
            bool actual;
            actual = sut.WaitForExit();
            Assert.AreEqual(expected, actual);
            string toWatchFor = "Exit Code";
            Assert.That(sut.StandardOutput.Contains(toWatchFor), "Standard Output did not contain expected text");
        }

        [Test]
        public void WaitForExitNeverStarted()
        {
            Assert.IsTrue(sut.WaitForExit());
        }

        [Test]
        public void WaitForExitStartFailed()
        {
            string program = "doesnotexist.exe";
            UtilityBackgroundProcess target = new UtilityBackgroundProcess(program);

            string arguments = String.Empty;
            bool expected = false;
            bool actual = target.Run(arguments);
            Assert.AreEqual(expected, actual);
            Assert.IsTrue(target.WaitForExit());
        }

        [Test]
        public void WaitForStandardOutputTestTrue()
        {
            string toWatchFor = "MockConsoleApp";
            TimeSpan maxWait = TimeSpan.FromSeconds(1);
            bool expected = true;
            bool actual;
            sut.Run("0 4");
            actual = sut.WaitForStandardOutput(toWatchFor, maxWait);
            Assert.AreEqual(expected, actual);
            Assert.IsTrue(sut.StandardOutput.Contains(toWatchFor));
        }

        [Test]
        public void WaitForStandardOutputTestFalse()
        {
            string toWatchFor = "Exit Code";
            TimeSpan maxWait = TimeSpan.FromSeconds(2);
            bool expected = false;
            bool actual;
            sut.Run("0 4");
            actual = sut.WaitForStandardOutput(toWatchFor, maxWait);
            Assert.AreEqual(expected, actual);
        }


        [Test]
        public void WaitForStandardOutputTestTrue2()
        {
            string toWatchFor = "Exit Code";
            bool expected = true;
            bool actual;
            sut.Run("0 1");
            Assert.IsFalse(sut.StandardOutput.Contains(toWatchFor));
            actual = sut.WaitForStandardOutput(toWatchFor);
            Assert.AreEqual(expected, actual);
            Assert.IsTrue(sut.StandardOutput.Contains(toWatchFor));
        }

        [Test]
        public void ExitCodeTest()
        {
            int expectedExitCode = 2;
            string args = expectedExitCode +" 0";
            sut.Run(args);
            sut.WaitForExit();
            Nullable<int> actual = sut.ExitCode;
            Assert.IsNotNull(actual);
            Assert.AreEqual(expectedExitCode, actual.Value);
        }

        private string tempText;
        [Test]
        public void DebugOutputTest()
        {
            tempText = String.Empty;
            sut.DebugMessageSent += new DebugMessageHandler(sut_DebugMessageSent);
            sut.Run("0 0");
            sut.WaitForExit();
            Debug.WriteLine(tempText);
            Assert.IsTrue(tempText.Contains("starting"));
        }

        void sut_DebugMessageSent(object sender, string message)
        {
            tempText += message + Environment.NewLine;
        }

        [Test]
        public void IsStoppedTestNeverStarted()
        {
            Assert.IsTrue(sut.IsStopped);
        }

        /// <summary>
        ///A test for IsStopped
        ///</summary>
        [Test]
        public void IsStoppedTest()
        {
            string program = string.Empty; // TODO: Initialize to an appropriate value
            UtilityBackgroundProcess target = new UtilityBackgroundProcess(program); // TODO: Initialize to an appropriate value
            bool actual;
            actual = target.IsStopped;
            Assert.Inconclusive("Verify the correctness of this test method.");
        }

        /// <summary>
        ///A test for StandardError
        ///</summary>
        [Test]
        public void StandardErrorTest()
        {
            string program = string.Empty; // TODO: Initialize to an appropriate value
            UtilityBackgroundProcess target = new UtilityBackgroundProcess(program); // TODO: Initialize to an appropriate value
            string actual;
            actual = target.StandardError;
            Assert.Inconclusive("Verify the correctness of this test method.");
        }

        /// <summary>
        ///A test for StandardOutput
        ///</summary>
        [Test]
        public void StandardOutputTest()
        {
            string program = string.Empty; // TODO: Initialize to an appropriate value
            UtilityBackgroundProcess target = new UtilityBackgroundProcess(program); // TODO: Initialize to an appropriate value
            string actual;
            actual = target.StandardOutput;
            Assert.Inconclusive("Verify the correctness of this test method.");
        }
    }
}
