using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using JobManagerInterfaces;
using System.Threading;
using System.Messaging;
using System.IO;

namespace JobManagerMonitor
{
    public partial class Form1 : Form
    {
        private MessageSendRecieve msr;

        public Form1()
        {
            InitializeComponent();
            msr = new MessageSendRecieve(AppConfig.ServerInbox, AppConfig.ServerOutbox);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            button1.Enabled = false;
            RefreshUI();
        }

        private void WaitForResponses(object arg)
        {
            object[] args = (object[])arg;
            MessageSendRecieve msr = (MessageSendRecieve)args[0];
            string msgID = (string)args[1];

            JobReportReturn jr = msr.WaitForStatus(msgID, TimeSpan.FromMinutes(1));

            if (jr != null)
            {
                Invoke(new MethodInvoker(delegate() { listViewStatus.BeginUpdate(); }));
                foreach (Job j in jr.Jobs.Keys)
                {
                    JobStatus js = jr.Jobs[j];
                    FullJob fj = new FullJob(j, js);
                    Invoke(new MethodInvoker(delegate()
                    {
                        ListViewItem lvi = listViewStatus.Items.Add(new ListViewItem(j.StartDate.ToString("yyyy/MM/dd HH:mm:ss")));
                        lvi.SubItems.Add(j.Configuration.ToString());
                        lvi.SubItems.Add(js.State.ToString());
                        if (js.Result != null)
                        {
                            lvi.SubItems.Add(js.Result.Success ? "Yes" : "No");
                        }
                        else
                        {
                            lvi.SubItems.Add("N/A");
                        }
                        lvi.SubItems.Add(j.JobXML);
                        if (js.Result != null && js.Result.Errors != null && js.Result.Errors.Count > 0)
                        {
                            lvi.SubItems.Add(js.Result.Errors[0]);
                        }
                        else
                        {
                            lvi.SubItems.Add(String.Empty);
                        }
                        lvi.Tag = fj;
                    }));
                }
                Invoke(new MethodInvoker(delegate() { listViewStatus.EndUpdate(); }));
            }
            Invoke(new MethodInvoker(delegate() { button1.Enabled = true; }));
        }

        private void RefreshUI()
        {
            listViewStatus.Items.Clear();
            
            string msgID = msr.RequestStatus(Environment.MachineName);

            Thread t = new Thread(new ParameterizedThreadStart(WaitForResponses));
            t.IsBackground = true;
            t.Start(new object[] { msr, msgID });
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            //RefreshUI();
        }

        private void listViewStatus_DoubleClick(object sender, EventArgs e)
        {
            if (listViewStatus.SelectedItems.Count > 0)
            {
                ListViewItem item = listViewStatus.SelectedItems[0];
                if (item.Tag != null && item.Tag is FullJob)
                {
                    FullJob fj = (FullJob)item.Tag;
                    JobStatus js = fj.Status;

                    if (js.Result != null && js.Result.Logs.Count > 0)
                    {
                        Dictionary<string, List<string>> logEntries = new Dictionary<string, List<string>>();

                        foreach (FileData log in js.Result.Logs)
                        {
                            using (MemoryStream ms = new MemoryStream(log.Data))
                            {
                                string lastDateTimeStr = null;
                                TextReader tr = new StreamReader(ms);
                                string line = null;
                                while ((line = tr.ReadLine()) != null)
                                {
                                    //[20100830 15:58:49] 
                                    string datetime = line.Substring(1, 17);
                                    if (System.Text.RegularExpressions.Regex.Match(datetime, @"\d{8}\s\d{2}:\d{2}:\d{2}").Success)
                                    {
                                        lastDateTimeStr = datetime;
                                        if (!logEntries.ContainsKey(datetime))
                                        {
                                            logEntries[datetime] = new List<string>();
                                        }
                                        if (!logEntries[datetime].Contains(line))
                                        {
                                            logEntries[datetime].Add(line);
                                        }
                                    }
                                    else
                                    {
                                        if (lastDateTimeStr != null)
                                        {
                                            logEntries[lastDateTimeStr].Add(line);
                                        }
                                    }
                                }
                            }
                        }

                        System.IO.FileInfo fi = new System.IO.FileInfo(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.InternetCache), Guid.NewGuid().ToString() + ".log"));
                        using (TextWriter tw = new StreamWriter(new FileStream(fi.FullName, FileMode.Create)))
                        {
                            List<string> keys = new List<string>(logEntries.Keys);
                            keys.Sort();
                            foreach (string key in keys)
                            {
                                foreach (string line in logEntries[key])
                                {
                                    tw.WriteLine(line);
                                }
                            }
                        }
                        System.Diagnostics.Process.Start("notepad.exe", "\"" + fi.FullName + "\"").WaitForExit();
                        try
                        {
                            fi.Delete();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.ToString());
                        }
                    }
                }
            }
        }

        private void listViewStatus_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                if (listViewStatus.SelectedItems.Count > 0)
                {
                    button1.Enabled = false;
                    foreach (ListViewItem item in listViewStatus.SelectedItems)
                    {
                        if (item.Tag != null && item.Tag is FullJob)
                        {
                            string jobID = ((FullJob)item.Tag).Job.JobID;
                            msr.DeleteJob(jobID);
                        }
                    }
                    Thread.Sleep(1000);
                    RefreshUI();
                }
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (listViewStatus.SelectedItems.Count > 0)
            {
                ListViewItem item = listViewStatus.SelectedItems[0];
                if (item.Tag != null && item.Tag is FullJob)
                {
                    FullJob fj = (FullJob)item.Tag;
                    JobStatus js = fj.Status;

                    FolderBrowserDialog fbd = new FolderBrowserDialog();
                    if (fbd.ShowDialog(this) == DialogResult.OK)
                    {
                        DirectoryInfo di = new DirectoryInfo(fbd.SelectedPath);
                        int i = 0;
                        foreach (ExecutionResult er in js.Result.ExecutionResults)
                        {
                            if (er.Attachments.Count > 0)
                            {
                                DirectoryInfo targetDir = new DirectoryInfo(Path.Combine(di.FullName, i.ToString()));
                                if (!targetDir.Exists)
                                {
                                    targetDir.Create();
                                }
                                foreach (FileData fd in er.Attachments)
                                {
                                    try
                                    {
                                        fd.WriteToDir(targetDir);
                                    }
                                    catch (IOException ex)
                                    {
                                        MessageBox.Show(ex.ToString());
                                    }
                                }
                            }
                            i++;
                        }
                    }
                }
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            button3.Enabled = false;
            string msgID = msr.RequestVMList(Environment.MachineName);

            Thread t = new Thread(new ParameterizedThreadStart(WaitForVMList));
            t.IsBackground = true;
            t.Start(new object[] { msr, msgID });
        }

        private void WaitForVMList(object arg)
        {
            object[] args = (object[])arg;
            MessageSendRecieve msr = (MessageSendRecieve)args[0];
            string msgID = (string)args[1];

            VMRequestReturn vmList = msr.WaitForVMList(msgID, TimeSpan.FromMinutes(5));

            if (vmList != null)
            {
                Dictionary<string, bool> vmMap = new Dictionary<string, bool>();
                foreach (string vm in vmList.VMPaths)
                {
                    vmMap[vm] = false;
                }
                foreach (string lockedVM in vmList.LockedVMs)
                {
                    vmMap[lockedVM] = true;
                }
                Invoke(new MethodInvoker(delegate() { ShowVMList(vmMap); }));
            }
            else
            {
                Invoke(new MethodInvoker(delegate() { ShowVMList("error while getting list of vms"); }));
            }
        }

        private void ShowVMList(Dictionary<string, bool> vmList)
        {
            VMListForm form = new VMListForm(msr);
            form.VMs = vmList;
            form.Show(this);
            button3.Enabled = true;
        }

        private void ShowVMList(string text)
        {
            button3.Enabled = true;
            PopupTextWindow tw = new PopupTextWindow();
            tw.InnerText = text;
            tw.Show();
        }

        private void buttonDetail_Click(object sender, EventArgs e)
        {
            if (listViewStatus.SelectedItems.Count > 0)
            {
                ListViewItem item = listViewStatus.SelectedItems[0];
                if (item.Tag != null && item.Tag is FullJob)
                {
                    FullJob fj = (FullJob)item.Tag;
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("Started: " + fj.Job.StartDate);
                    sb.AppendLine("Configuration: " + fj.Job.Configuration);
                    sb.AppendLine("VM: " + fj.Status.VMPath);
                    sb.AppendLine("Job ID: " + fj.Job.JobID);
                    sb.AppendLine("JobXML: " + fj.Job.JobXML);
                    sb.AppendLine("SequenceXML: " + fj.Job.SequenceXML);
                    sb.AppendLine("ConfigXML: " + fj.Job.ConfigurationXML);
                    sb.AppendLine("Packages (" + fj.Job.Packages.Count + "):");
                    for (int i = 0; i < fj.Job.Packages.Count; i++)
                    {
                        ExecutablePackage ep = fj.Job.Packages[i];
                        sb.AppendLine("PackageXML: " + ep.PackageXML);
                        sb.AppendLine("  DLL: " + ep.JobRunnerDLLName);
                        if (ep.Properties.Count > 0)
                        {
                            sb.AppendLine("  Package Properties:");
                            foreach(string key in ep.Properties.Keys)
                            {
                                sb.AppendLine("    " + key + ": " + ep.Properties[key]);
                            }
                        }
                        if (fj.Status.Result != null && fj.Status.Result.ExecutionResults.Count > i)
                        {
                            ExecutionResult er = fj.Status.Result.ExecutionResults[i];
                            sb.AppendLine("  Success: " + er.Success);
                            sb.AppendLine("  Attachments (" + er.Attachments.Count + "):");
                            foreach (FileData fd in er.Attachments)
                            {
                                sb.AppendLine("    Name: " + fd.Name);
                            }
                            sb.AppendLine("  Errors (" + er.Errors.Count + "):");
                            foreach (string error in er.Errors)
                            {
                                sb.AppendLine("    " + error);
                            }
                        }
                    }
                    sb.AppendLine("State: " + fj.Status.State);
                    sb.AppendLine("Last State Change: " + fj.Status.LastStateChange);
                    if (fj.Status.Result != null)
                    {
                        sb.AppendLine("Completed: " + fj.Status.Result.Completed);
                        sb.AppendLine("Success: " + fj.Status.Result.Success);
                        sb.AppendLine("SnapshotOnShutdown: " + fj.Status.Result.SnapshotOnShutdown);
                        if (fj.Status.Result.SnapshotOnShutdown)
                        {
                            sb.AppendLine("SnapshotName: " + fj.Status.Result.SnapshotName);
                            sb.AppendLine("SnapshotDesc: " + fj.Status.Result.SnapshotDesc);
                        }
                        sb.AppendLine("Log Files (" + fj.Status.Result.Logs.Count + "):");
                        foreach (FileData fd in fj.Status.Result.Logs)
                        {
                            sb.AppendLine("  Name: " + fd.Name);
                        }
                    }
                    if (fj.Job.Properties != null && fj.Job.Properties.Count > 0)
                    {
                        sb.AppendLine("Properties:");
                        foreach (string key in fj.Job.Properties.Keys)
                        {
                            sb.AppendLine(key + ": " + fj.Job.Properties[key]);
                        }
                    }

                    //sb.AppendLine(Utility.DumpObject(fj.Job, 0, typeof(ExecutablePackage), typeof(IEnumerable<ExecutablePackage>)));
                    MessageBox.Show(sb.ToString());
                    //MessageBox.Show(Utility.DumpObject(js, 0, typeof(JobResult), typeof(List<FileData>), typeof(List<ExecutionResult>), typeof(ExecutionResult)));
                }
            }
        }

        internal class FullJob
        {
            public Job Job;
            public JobStatus Status;

            public FullJob(Job j, JobStatus js)
            {
                this.Job = j;
                this.Status = js;
            }
        }

        private void listViewStatus_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (e.Column == 0)
            {
                if (listViewStatus.Sorting == SortOrder.Ascending)
                {
                    listViewStatus.Sorting = SortOrder.Descending;
                }
                else
                {
                    listViewStatus.Sorting = SortOrder.Ascending;
                }
                listViewStatus.Sort();
            }
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            if (listViewStatus.SelectedItems.Count > 0)
            {
                ListViewItem item = listViewStatus.SelectedItems[0];
                if (item.Tag != null && item.Tag is FullJob)
                {
                    if (((FullJob)item.Tag).Status.State != JobStates.JobFinishedSent && ((FullJob)item.Tag).Status.State != JobStates.JobFinishedNotSent)
                    {
                        button1.Enabled = false;
                        string jobID = ((FullJob)item.Tag).Job.JobID;
                        msr.CancelJob(jobID);
                        Thread.Sleep(500);
                        RefreshUI();
                    }
                }
            }
        }

        private void buttonResendResult_Click(object sender, EventArgs e)
        {
            if (listViewStatus.SelectedItems.Count > 0)
            {
                ListViewItem item = listViewStatus.SelectedItems[0];
                if (item.Tag != null && item.Tag is FullJob)
                {
                    FullJob job = (FullJob)item.Tag;
                    AutomationMessage m = new AutomationMessage(job.Job.OriginalHost, job.Job.OriginalMessageID,  new JobCompleted(job.Job, job.Status.Result));
                    msr.SendToHost(m);
                }
            }
        }
    }
}
