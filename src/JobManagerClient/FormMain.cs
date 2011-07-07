using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;
using System.Reflection;
using JobManagerInterfaces;
using System.IO;
using System.Management;

namespace JobManagerClient
{
    public partial class FormMain : Form, ISystem
    {
        
        private JobClient jobClient;
        public bool TestOnly;
        

        public FormMain()
        {
            jobClient = new JobClient(this);
            InitializeComponent();
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            jobClient.Start();
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
            }
        }

        private void notifyIcon1_DoubleClick(object sender, EventArgs e)
        {
            Restore();
        }

        private void notifyIcon1_Click(object sender, EventArgs e)
        {
            Restore();
        }

        private void Restore()
        {
            if (WindowState == FormWindowState.Minimized)
            {
                Show();
                WindowState = FormWindowState.Normal;
            }
            Activate();
        }

        public void UpdateStatus(string text)
        {
            UpdateStatus(text, true);
        }

        public void UpdateStatus(string text, bool log)
        {
            if (log)
            {
                Logger.Instance.LogString(text);
            }
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(delegate() { UpdateStatus(text, false); }));
            }
            else
            {
                textBoxStatus.Text = text;
                notifyIcon1.ShowBalloonTip(1000, String.Empty, text, ToolTipIcon.None);
            }
        }

        public void Shutdown(bool restart)
        {
            ManagementBaseObject mboShutdown = null;
            ManagementClass mcWin32 = new ManagementClass("Win32_OperatingSystem");
            mcWin32.Get();

            // You can't shutdown without security privileges
            mcWin32.Scope.Options.EnablePrivileges = true;
            ManagementBaseObject mboShutdownParams =
                     mcWin32.GetMethodParameters("Win32Shutdown");

            // Flag 1 means we want to shut down the system. Use "2" to reboot.
            if (restart)
            {
                mboShutdownParams["Flags"] = "2";
            }
            else
            {
                mboShutdownParams["Flags"] = "1";
            }
            mboShutdownParams["Reserved"] = "0";
            foreach (ManagementObject manObj in mcWin32.GetInstances())
            {
                mboShutdown = manObj.InvokeMethod("Win32Shutdown",
                                               mboShutdownParams, null);
            }
        }

        private void buttonStop_Click(object sender, EventArgs e)
        {
            jobClient.Stop();
            Close();
        }

        private void FormMain_FormClosed(object sender, FormClosedEventArgs e)
        {
            //Logger.Instance.Flush();
        }

        private void buttonViewLog_Click(object sender, EventArgs e)
        {
            //Logger.Instance.Flush();
            Process p = new Process();
            p.StartInfo.FileName = "notepad.exe";
            p.StartInfo.Arguments = Logger.Instance.FileName;
            p.Start();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            jobClient.Start();
        }

        private void buttonStop_Click_1(object sender, EventArgs e)
        {
            jobClient.Stop();
        }

        private void buttonRunPackage_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "*.dll|*.dll";
            ofd.Title = "Select Package DLL File";
            if (ofd.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
            {
                Job j = new Job();
                j.Properties = new SerializableDictionary<string, string>();
                j.StartDate = DateTime.Now;
                j.Packages = new List<ExecutablePackage>();
                FileInfo epFile = new FileInfo(ofd.FileName);
                ExecutablePackage ep = new ExecutablePackage();
                ep.ContentDirectory = epFile.Directory.FullName;
                ep.JobRunnerDLLName = epFile.Name;
                ep.JobRunnerClassName = "FILL ME IN";
                ep.Properties = new SerializableDictionary<string,string>();
                ep.Properties.Add("ExamplePropertyKey", "ExamplePropertyValue");
                j.ISOs = new SerializableDictionary<string, string>();
                j.ISOs["ExampleISOName"] = @"c:\some\path\to.iso";
                j.Packages.Add(ep);
                FileInfo jobXMLFile = new FileInfo(Path.Combine(Utilities.ExecutingAssembly.Directory.FullName, "job.xml"));
                if (jobXMLFile.Exists)
                {
                    jobXMLFile.MoveTo(Path.Combine(jobXMLFile.Directory.FullName, Guid.NewGuid().ToString() + "job.xml"));
                }
                //save job as xml file
                using (TextWriter tw = new StreamWriter(jobXMLFile.FullName, false))
                {
                    tw.Write(j.ToXML(true));
                }
                Process.Start("notepad.exe", jobXMLFile.FullName);
            }
        }
    }
}
