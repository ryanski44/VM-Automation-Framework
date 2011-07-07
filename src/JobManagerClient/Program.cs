using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Diagnostics;

namespace JobManagerClient
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                if (args[0] == "postrestart")
                {
                    Process p = new Process();
                    p.StartInfo.FileName = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    p.StartInfo.Arguments = "wait";
                    p.Start();
                    Application.Exit();
                    return;
                }
                else if (args[0] == "wait")
                {
                    System.Threading.Thread.Sleep(10000);
                }
            }
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FormMain());
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                Exception ex = (Exception)e.ExceptionObject;
                try
                {
                    Logger.Instance.LogString(ex.ToString());
                }
                catch (Exception)
                {
                    //eat it
                }

                MessageBox.Show("An unhandled exception has occurred and has been logged to the application log (" + Logger.Instance.FileName + ").  The program will now exit.  Excpetion: " + ex.Message, "Fatal Error");
            }
            finally
            {
                Application.Exit();
            }
        }
    }
}
