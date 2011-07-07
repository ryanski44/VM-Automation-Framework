using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Configuration;

namespace JobManagerClient
{
    public class AppConfig
    {

        public static DirectoryInfo ServerInbox { get { return new DirectoryInfo(ConfigurationManager.AppSettings["inbox"]); } }
        public static DirectoryInfo ServerOutbox { get { return new DirectoryInfo(ConfigurationManager.AppSettings["outbox"]); } }

        public static string MagicDiscPath
        {
            get { return ProgramFilesx86 + @"\MagicDisc"; }
        }

        public static string MISOPath
        {
            get { return Utilities.ExecutingAssembly.Directory.FullName + @"\miso.exe"; }
        }

        public static string ProgramFilesx86
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

        public static bool ArchitectureIs64Bit
        {
            get
            {
                return !String.IsNullOrEmpty(Environment.GetEnvironmentVariable("ProgramFiles(x86)"));
            }
        }
    }
}
