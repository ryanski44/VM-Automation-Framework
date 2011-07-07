using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;

namespace JobManagerService
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : Installer
    {
        public ProjectInstaller()
        {
            InitializeComponent();
        }

        public override void Install(IDictionary savedState)
        {
            base.Install(savedState);
            InstallContext context = this.Context;
            string user = context.Parameters["ServiceUser"];
            string password = context.Parameters["ServicePassword"];
            if (String.IsNullOrEmpty(user) || String.IsNullOrEmpty(password))
            {
                return;
            }
            Process p = Process.Start("sc", "config JobManager obj= " + user + " password= " + password);
            p.WaitForExit();
            if (p.ExitCode != 0)
            {
                Console.WriteLine("Could not set user/pass for service");
                Console.WriteLine(p.StandardOutput.ReadToEnd());
            }
        }
    }
}
