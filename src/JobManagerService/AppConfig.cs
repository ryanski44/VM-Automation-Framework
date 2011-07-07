using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;
using System.IO;

namespace JobManagerService
{
    public class AppConfig
    {
        public static DirectoryInfo ExecutingDir { get { return (new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location)).Directory; } }

        public static DirectoryInfo Inbox { get { return new DirectoryInfo(ConfigurationManager.AppSettings["inbox"]); } }
        public static DirectoryInfo Outbox { get { return new DirectoryInfo(ConfigurationManager.AppSettings["outbox"]); } }
        public static DirectoryInfo FileDrop { get { return new DirectoryInfo(ConfigurationManager.AppSettings["filedrop"]); } }

        public static string VIXPath { get { return ConfigurationManager.AppSettings["vixPath"]; } }
        public static string VSphereHost { get { return ConfigurationManager.AppSettings["vSphereHost"]; } }
        public static string VSphereUser { get { return ConfigurationManager.AppSettings["vSphereUser"]; } }
        public static string VSpherePass { get { return ConfigurationManager.AppSettings["vSpherePass"]; } }

        public static int MaxVMsAtOnce
        {
            get
            {
                int maxVMs = 2; //default
                Int32.TryParse(ConfigurationManager.AppSettings["maxvms"], out maxVMs);
                return maxVMs;
            }
        }

        public static int JobTimeoutMins
        {
            get
            {
                int timeoutMins = 120; //default
                Int32.TryParse(ConfigurationManager.AppSettings["jobtimeout_mins"], out timeoutMins);
                return timeoutMins;
            }
        }

        public static int JobLifeTimeDays
        {
            get
            {
                int jobLifetimeDays = 3; //default
                Int32.TryParse(ConfigurationManager.AppSettings["joblifetime_days"], out jobLifetimeDays);
                return jobLifetimeDays;
            }
        }
    }
}
