using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace JobManagerMonitor
{
    public partial class PopupTextWindow : Form
    {
        public PopupTextWindow()
        {
            InitializeComponent();
        }

        public string InnerText
        {
            get { return textBox1.Text; }
            set { textBox1.Text = value; }
        }
    }
}
