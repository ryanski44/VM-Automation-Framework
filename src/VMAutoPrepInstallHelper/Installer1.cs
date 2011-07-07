using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;
using System.IO;
using IWshRuntimeLibrary;

namespace VMAutoPrepInstallHelper
{
    [RunInstaller(true)]
    public partial class Installer1 : Installer
    {
        private static string MagicDiscPath
        {
            get { return ProgramFilesx86 + @"\MagicDisc"; }
        }

        private static string ProgramFilesx86
        {
            get
            {
                if (ArchitectureIs64Bit)
                {
                    return Environment.GetEnvironmentVariable("ProgramFiles(x86)");
                }
                else
                {
                    return Environment.GetEnvironmentVariable("ProgramFiles");
                }
            }
        }

        private static bool ArchitectureIs64Bit
        {
            get
            {
                return !String.IsNullOrEmpty(Environment.GetEnvironmentVariable("ProgramFiles(x86)"));
            }
        }

        private static DirectoryInfo ExecutingDir
        {
            get { return (new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location)).Directory; }
        }

        public Installer1()
        {
            InitializeComponent();
        }

        public override void Install(IDictionary stateSaver)
        {
            base.Install(stateSaver);

            if (!Directory.Exists(MagicDiscPath))
            {
                string magicDiscPath = null;

                if (ArchitectureIs64Bit)
                {
                    magicDiscPath = Path.Combine(ExecutingDir.FullName, "setup_magicdiscx64.exe");
                }
                else
                {
                    magicDiscPath = Path.Combine(ExecutingDir.FullName, "setup_magicdisc.exe");
                }

                Process p = Process.Start(magicDiscPath, "/s");

                //a driver warning will pop up on vista+ systems at this point.  The user will have to accept this.
                //The magic disc installer does not give useful return codes, so we can't do anything except assume the installation
                //was successful

                p.WaitForExit();
            }
            
            Process.Start("net.exe", "user Testing Testing /add").WaitForExit();
            Process.Start("net.exe", "localgroup Administrators Testing /add").WaitForExit();
            Process.Start("net.exe", "accounts /maxpwage:unlimited").WaitForExit();

            //create a shortcut in the all users desktop to the setup_for_snapshot.bat batch file
            WshShellClass wshShell = new WshShellClass();

            object allUsersDesktop = "AllUsersDesktop";
            string shortcutPath = wshShell.SpecialFolders.Item( ref allUsersDesktop ).ToString( );

            IWshShortcut myShortcut = (IWshShortcut)wshShell.CreateShortcut(Path.Combine(shortcutPath, "Automation - Setup For Snapshot.lnk"));
            myShortcut.TargetPath = @"c:\automation\setup_for_snapshot.bat";
            myShortcut.WorkingDirectory = @"c:\automation";
            myShortcut.Description = "Automation - Setup For Snapshot";
            myShortcut.Save();
        }

        public override void Commit(IDictionary savedState)
        {
            base.Commit(savedState);
        }

        public override void Rollback(IDictionary savedState)
        {
            Process.Start("net.exe", "user Testing /delete").WaitForExit();
            base.Rollback(savedState);
        }

        public override void Uninstall(IDictionary savedState)
        {
            Process.Start("net.exe", "user Testing /delete").WaitForExit();
            base.Uninstall(savedState);
        }
    }
}
