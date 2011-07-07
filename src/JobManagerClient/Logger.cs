using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using JobManagerInterfaces;

namespace JobManagerClient
{
    public class Logger : StatusLogger
    {
        private static object writeLock = new object();
        private static Logger _inst;
        public static Logger Instance
        {
            get
            {
                if (_inst == null)
                {
                    _inst = new Logger();
                }
                return _inst;
            }
        }

        private TextWriter tw;
        private string fileName;
        private bool paused;
        private List<string> buffer;

        private Logger()
        {
            FileInfo assemblyFile = new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location);
            fileName = Path.Combine(assemblyFile.Directory.FullName, DateTime.Now.ToString("yyyyMMdd HH.mm.ss") + ".log");
            buffer = new List<string>();
            paused = true;
            Resume();
        }

        public void Resume()
        {
            if (paused)
            {
                lock (writeLock)
                {
                    tw = new StreamWriter(fileName, true);
                    paused = false;
                }
                foreach (string str in buffer)
                {
                    LogString(str);
                }
                buffer.Clear();
            }
        }

        public void Pause()
        {
            lock (writeLock)
            {
                tw.Close();
                tw = null;
                paused = true;
            }
        }

        public string FileName
        {
            get
            {
                return fileName;
            }
        }

        public void LogString(string str)
        {
            lock (writeLock)
            {
                if (paused)
                {
                    buffer.Add(str);
                }
                else
                {
                    tw.WriteLine("[" + DateTime.Now.ToString("yyyyMMdd HH:mm:ss") + "] " + str);
                    tw.Flush();
                }
            }
        }
    }
}
