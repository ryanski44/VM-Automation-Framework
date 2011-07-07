using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Configuration;

namespace JobManagerMonitor
{
    public static class AppConfig
    {
        public static DirectoryInfo ServerInbox { get { return new DirectoryInfo(ConfigurationManager.AppSettings["inbox"]); } }
        public static DirectoryInfo ServerOutbox { get { return new DirectoryInfo(ConfigurationManager.AppSettings["outbox"]); } }
    }
}