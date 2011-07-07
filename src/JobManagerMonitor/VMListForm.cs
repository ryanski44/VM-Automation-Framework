using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using JobManagerInterfaces;

namespace JobManagerMonitor
{
    public partial class VMListForm : Form
    {
        private MessageSendRecieve msr;
        private Dictionary<string, bool> vms;

        public VMListForm(MessageSendRecieve msr)
        {
            vms = new Dictionary<string, bool>();
            this.msr = msr;
            InitializeComponent();
            RefreshUI();
        }

        public Dictionary<string, bool> VMs
        {
            get { return vms; }
            set
            {
                vms = value;
                RefreshUI();
            }
        }

        private void RefreshUI()
        {
            lvVMs.Items.Clear();
            foreach (string vm in vms.Keys)
            {
                ListViewItem item = new ListViewItem(vm);
                item.SubItems.Add(vms[vm] ? "Yes" : "No");
                item.Tag = vm;
                lvVMs.Items.Add(item);
            }
        }

        private void buttonLock_Click(object sender, EventArgs e)
        {
            if (lvVMs.SelectedItems.Count > 0)
            {
                string vm = lvVMs.SelectedItems[0].Tag as string;
                bool locked = vms[vm];
                if (locked)
                {
                    msr.UnLockVM(vm);
                    vms[vm] = false;
                }
                else
                {
                    msr.LockVM(vm);
                    vms[vm] = true;
                }
                RefreshUI();
            }
        }
    }
}
