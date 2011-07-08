using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.IO;
using JobManagerInterfaces;

namespace RunBVTTests
{
    public class Main : JobRunner
    {
        public ExecutionResult Execute(JobClientInterface jci)
        {
            try
            {
                jci.LogString("Running Tests");
                return Testing.RunTests(jci);
            }
            catch (ThreadAbortException)
            {
                return null;
            }
            catch (Exception e)
            {
                jci.LogString("Exception thrown: " + e.Message);
                jci.LogString(e.ToString());

                try
                {
                    string body = "An Error Occured during execution:" + Environment.NewLine + e.ToString();

                    List<FileData> attachments = new List<FileData>();
                    if (File.Exists("error.png"))
                    {
                        attachments.Add(FileData.FromFile(new FileInfo("error.png")));
                    }

                    return new ExecutionResult(body, attachments);
                }
                catch (Exception ex)
                {
                    jci.LogString(ex.ToString());
                }
            }
            return null;
        }
    }
}
