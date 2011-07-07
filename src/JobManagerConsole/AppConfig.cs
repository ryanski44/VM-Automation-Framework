using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Configuration;

namespace JobManagerConsole
{
    public static class AppConfig
    {
        public static DirectoryInfo ServerInbox { get { return new DirectoryInfo(ConfigurationManager.AppSettings["inbox"]); } }
        public static DirectoryInfo ServerOutbox { get { return new DirectoryInfo(ConfigurationManager.AppSettings["outbox"]); } }
        public static string RunAfterJob { get { return ConfigurationManager.AppSettings["runafterjob"]; } }
    }
}
