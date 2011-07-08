using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace ProcessUtilities
{
    public delegate void DebugMessageHandler(object sender, string message);

    public class UtilityBackgroundProcess
    {
        public event DebugMessageHandler DebugMessageSent; 

        private readonly object OutLock = new object();
        private readonly object ErrorLock = new object();
        public Process proc;
        private string program;
        private string standardOut;
        private bool standardOutDone;
        private string standardError;
        private bool standardErrorDone;

        private const int DEF_TIMEOUT_SHORT = 4000;
        private TimeSpan DEF_TIMEOUT_WAIT = TimeSpan.FromMinutes(4);

        private void onDebugMessageSent(string message)
        {
            if(DebugMessageSent != null)
            {
                DebugMessageSent(this, message);
            }
        }

        public string ProgramEXE
        {
            get { return program; }
        }

        public bool IsStopped
        {
            get
            {
                if (proc != null)
                {
                    return HasExited;
                }
                else
                {
                    return true;
                }
            }
        }

        private bool HasExited
        {
            get
            {
                try
                {
                    return proc.HasExited;
                }
                catch (InvalidOperationException ex)
                {
                    onDebugMessageSent(ex.ToString());
                    return true;
                }
            }
        }

        public int? ExitCode
        {
            get
            {
                try
                {
                    if (proc != null && proc.HasExited)
                    {
                        return proc.ExitCode;
                    }
                    else
                    {
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    onDebugMessageSent(ex.ToString());
                    return null;
                }
            }
        }

        public string StandardOutput
        {
            get
            {
                lock (OutLock)
                {
                    return standardOut;
                }
            }
        }

        public string StandardError
        {
            get
            {
                lock (ErrorLock)
                {
                    return standardError;
                }
            }
        }

        public UtilityBackgroundProcess(string program)
        {
            this.program = program;
            this.standardOut = String.Empty;
            this.standardError = String.Empty;
            this.standardOutDone = false;
            this.standardErrorDone = false;
            proc = null;
        }


        public bool Run(string arguments)
        {
            return Run(arguments, null);
        }

        public bool Run(string arguments, string workingDir)
        {
            if (!IsStopped)
            {
                throw new InvalidOperationException("The process is already started!");
            }
            try
            {
                proc = new Process();
                if (workingDir != null)
                {
                    proc.StartInfo.WorkingDirectory = workingDir;
                }
                else
                {
                    proc.StartInfo.WorkingDirectory = Directory.GetCurrentDirectory();
                }
                proc.StartInfo.FileName = program;
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.RedirectStandardInput = false;
                if (arguments != null)
                {
                    proc.StartInfo.Arguments = arguments;
                }
                proc.StartInfo.CreateNoWindow = true;
                proc.OutputDataReceived += new DataReceivedEventHandler(proc_OutputDataReceived);
                proc.ErrorDataReceived += new DataReceivedEventHandler(proc_ErrorDataReceived);
                onDebugMessageSent("UtilityBackgroundProcess starting: \"" + proc.StartInfo.FileName + "\" " + proc.StartInfo.Arguments);
                bool startval = proc.Start();
                if (!startval)
                {
                    onDebugMessageSent("Process launch error");
                    proc = null;
                    return false;
                }
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                
            }
            catch(Exception ex)
            {
                onDebugMessageSent(ex.ToString());
                proc = null;
                return false;
            }
            return true;
        }

        private void proc_ErrorDataReceived(object sender, DataReceivedEventArgs outLine)
        {
            lock (ErrorLock)
            {
                if (outLine.Data == null)
                {
                    standardErrorDone = true;
                }
                else if (outLine.Data.Length > 0)
                {
                    standardError += outLine.Data + Environment.NewLine;
                }
            }
        }

        private void proc_OutputDataReceived(object sendingProcess, DataReceivedEventArgs outLine)
        {
            lock (OutLock)
            {
                if (outLine.Data == null)
                {
                    standardOutDone = true;
                }
                else if (outLine.Data.Length > 0)
                {
                    standardOut += outLine.Data + Environment.NewLine;
                }
            }
        }

        private bool FlushOutputs(TimeSpan maxWaitTime)
        {
            DateTime start = DateTime.Now;
            while (!standardErrorDone || !standardOutDone)
            {
                if (DateTime.Now.Subtract(start) > maxWaitTime)
                {
                    return false;
                }
                Thread.Sleep(500);
            }
            return true;
        }

        //returns true if program exits on its own before default timeout
        //returns false if program had to be forced to exit after default timeout
        public bool WaitForExit()
        {
            return WaitForExit(DEF_TIMEOUT_WAIT);
        }

        //returns true if program exits on its own before timeout
        //returns false if program had to be forced to exit after timeout
        public bool WaitForExit(TimeSpan timeout)
        {
            if (proc == null || proc.HasExited)
            {
                return true;
            }
            DateTime start = DateTime.Now;
            onDebugMessageSent("Waiting " + timeout.TotalSeconds + " seconds for \"" + program + "\" to terminate");
            if (proc.WaitForExit((int)timeout.TotalMilliseconds))
            {
                if (FlushOutputs(timeout.Subtract(DateTime.Now.Subtract(start))))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                onDebugMessageSent(program + " has not exited within " + timeout.TotalSeconds + " seconds, stopping process...");
                Stop();
                return false;
            }
        }

        public bool WaitForStandardOutput(string toWatchFor)
        {
            return WaitForStandardOutput(toWatchFor, DEF_TIMEOUT_WAIT);
        }

        public bool WaitForStandardOutput(string toWatchFor, TimeSpan maxWait)
        {
            TimeSpan pollingInterval = TimeSpan.FromMilliseconds(500);
            int count = 0;
            while (!StandardOutput.Contains(toWatchFor))
            {
                count++;
                Thread.Sleep(pollingInterval);
                if (pollingInterval.Ticks * count > maxWait.Ticks)
                {
                    return false;
                }
            }
            Debug.WriteLine("Actual Wait Time: " + pollingInterval.Milliseconds * count + " milliseconds");
            return true;
        }

        public void Stop()
        {
            if (proc != null)
            {
                if (!HasExited)
                {
                    onDebugMessageSent(string.Format("Stopping process {0}", program));
                    proc.CloseMainWindow();
                    proc.WaitForExit(DEF_TIMEOUT_SHORT);
                    if (!proc.HasExited)
                    {
                        proc.Kill();
                    }
                }
            }
        }

        public void WaitForSubProcessToExit()
        {
            DateTime started = DateTime.Now;

            Console.Write(".");
            foreach (Process subProc in GetChildProcesses(proc.Id))
            {
                try
                {
                    if (!subProc.HasExited)
                    {
                        subProc.WaitForExit();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                }
            }
        }

        public static int? GetParentProcessID(Process p)
        {
            PerformanceCounter pc = new PerformanceCounter("Process", "Creating Process Id", p.ProcessName);
            try
            {
                int parentProcessID = (int)pc.RawValue;
                return parentProcessID;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
            return null;
        }

        public static Process[] GetChildProcesses(int processID)
        {
            List<Process> output = new List<Process>();
            foreach (Process p in Process.GetProcesses())
            {
                if (p.Id == processID)
                {
                    continue;
                }
                int? parentProcessID = GetParentProcessID(p);
                if (parentProcessID != null && parentProcessID.Value == processID)
                {
                    output.Add(p);
                }
                else
                {
                    p.Dispose();
                }
            }
            return output.ToArray();
        }

        ~UtilityBackgroundProcess()
        {
            try
            {
                Stop();
            }
            catch { }
            try
            {
                if (proc != null)
                {
                    proc.Close();
                }
            }
            catch { }
        }
    }

}
