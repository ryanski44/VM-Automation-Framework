using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using System.Management;

namespace JobManagerClient
{
    public static class Utilities
    {
        private static string startupOnceRegPath = @"Software\Microsoft\Windows\CurrentVersion\RunOnce";

        public static void AddStartEntry(string name, string fileName)
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(startupOnceRegPath, true))
            {
                if (key != null)
                {
                    key.SetValue(name, fileName);
                }
            }
        }

        public static FileInfo ExecutingAssembly
        {
            get { return new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location); }
        }

        public static DriveInfo FindMagicISODrive()
        {
            foreach (DriveInfo di in DriveInfo.GetDrives())
            {
                try
                {
                    if(di.DriveType == DriveType.CDRom && !di.IsReady)
                    {
                        return di;
                    }
                }
                catch (Exception)
                {
                    //eat it
                }
            }
            return null;
        }
    }
}
