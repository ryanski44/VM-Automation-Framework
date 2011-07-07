using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32;

namespace VMAutoPrepInstallHelper
{
    public static class RegistryHelper
    {
        private static string startupOnceRegPath = @"Software\Microsoft\Windows\CurrentVersion\RunOnce";
        private static string startupRegPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static void AddStartOnceEntry(string name, string fileName)
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(startupOnceRegPath, true))
            {
                if (key != null)
                {
                    key.SetValue(name, fileName);
                }
            }
        }

        public static void AddStartEntry(string name, string fileName)
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(startupRegPath, true))
            {
                if (key != null)
                {
                    key.SetValue(name, fileName);
                }
            }
        }

        public static void SetRegistryLMValue(string keyPath, string name, object value)
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath, true))
            {
                if (key != null)
                {
                    key.SetValue(name, value);
                }
            }
        }

        //public static RegistryKey LocalMachineBaseKey
        //{
        //    get
        //    {
        //        RegistryKey localMachine = Registry.LocalMachine;
        //        if (Installer1.ArchitectureIs64Bit)
        //        {
        //            localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        //        }
        //    }
        //}
    }
}
