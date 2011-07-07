using System;
using System.Collections.Generic;
using System.Text;
using JobManagerInterfaces;
//using JobManagerInterfaces.XML;
using System.Configuration;
using System.IO;
using System.Xml;
using System.Xml.Schema;

namespace JobManagerConsole
{
    class Program
    {
        public static DirectoryInfo ASSEMBLY_DIR = (new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location)).Directory;

        public static int Main(string[] args)
        {
            try
            {
                if (args.Length == 2 || args.Length == 3)
                {
                    string pathToBuildOutput = args[0];
                    string pathToJobCollectionXML = args[1];

                    if (!File.Exists(pathToJobCollectionXML))
                    {
                        pathToJobCollectionXML = Path.Combine(pathToBuildOutput, pathToJobCollectionXML);
                    }

                    JobCollectionSet set = new JobCollectionSet(new FileInfo(pathToJobCollectionXML));
                    List<Job> jobs = set.CreateJobs(pathToBuildOutput);

                    MessageSendRecieve msr = new MessageSendRecieve(AppConfig.ServerInbox, AppConfig.ServerOutbox);

                    List<string> sentMessages = new List<string>();

                    foreach (Job j in jobs)
                    {
                        Console.WriteLine("Queueing Job " + j.JobID + " for config " + j.Configuration);
                        sentMessages.Add(msr.QueueJob(j));
                    }

                    string resultBasePath = Path.Combine(ASSEMBLY_DIR.FullName, "result");
                    if (args.Length == 3)
                    {
                        string outputPath = args[2];
                        if (Directory.Exists(outputPath))
                        {
                            resultBasePath = outputPath;
                        }
                    }

                    if (!Directory.Exists(resultBasePath))
                    {
                        Directory.CreateDirectory(resultBasePath);
                    }

                    Console.WriteLine("Waiting for Jobs to Finish");

                    bool executionSuccessOverall = true;

                    foreach (string sentMessageID in sentMessages)
                    {
                        JobCompleted jobComplete = msr.WaitForJobCompletion(sentMessageID, TimeSpan.FromDays(2));

                        string resultDirPath = Path.Combine(resultBasePath, jobComplete.Job.Configuration + "_" + jobComplete.Job.JobID);

                        DirectoryInfo resultDir = new DirectoryInfo(resultDirPath);
                        if (!resultDir.Exists)
                        {
                            resultDir.Create();
                        }

                        using (TextWriter tw = new StreamWriter(Path.Combine(resultDir.FullName, "result.xml")))
                        {
                            tw.Write(jobComplete.Result.ToXML());
                        }
                        using (TextWriter tw = new StreamWriter(Path.Combine(resultDir.FullName, "job.xml")))
                        {
                            tw.Write(jobComplete.Job.ToXML());
                        }

                        Console.WriteLine("Job: " + jobComplete.Job.JobID);
                        Console.WriteLine("Success = " + jobComplete.Result.Success);

                        foreach (ExecutionResult er in jobComplete.Result.ExecutionResults)
                        {
                            if (er.Errors.Count > 0)
                            {
                                Console.WriteLine("Errors:");
                                foreach (var error in er.Errors)
                                {
                                    Console.WriteLine(error);
                                }
                            }
                            Console.WriteLine(er.Attachments.Count + " attachments");
                            foreach (FileData attachement in er.Attachments)
                            {
                                attachement.WriteToDirRename(resultDir);
                            }
                        }

                        int i = 0;
                        if (jobComplete.Result.Logs != null)
                        {
                            foreach (FileData logData in jobComplete.Result.Logs)
                            {
                                if (logData.Data.Length > 0)
                                {
                                    logData.WriteToFile(new FileInfo(Path.Combine(resultDir.FullName, "log" + i + ".txt")));
                                    i++;
                                }
                            }
                        }

                        if (!jobComplete.Result.Completed || jobComplete.Result.Errors.Count > 0)
                        {
                            executionSuccessOverall = false;
                        }
                    }
                    string[] afterJobExeParts = AppConfig.RunAfterJob.Split(' ');
                    if (afterJobExeParts.Length >= 2)
                    {
                        string exeFileName = afterJobExeParts[0];
                        if (!File.Exists(exeFileName))
                        {
                            exeFileName = Path.Combine(ASSEMBLY_DIR.FullName, exeFileName);
                        }
                        if (File.Exists(exeFileName))
                        {
                            string args2 = string.Join(" ", afterJobExeParts, 1, afterJobExeParts.Length - 1);
                            args2 = args2.Replace("$(JobResultPath)", resultBasePath);
                            using (System.Diagnostics.Process p = System.Diagnostics.Process.Start(exeFileName, args2))
                            {
                                p.WaitForExit();
                            }
                        }
                    }

                    if (executionSuccessOverall)
                    {
                        return 0;
                    }
                    else
                    {
                        return -1;
                    }
                }
                else
                {
                    Console.WriteLine("Usage: JobManagerConsole.exe [Path_To_Build_Output] [Path_To_JobCollection_XML] ([OutputPath])");
                    Console.WriteLine();
                    //Console.WriteLine(@"Example: JobManagerConsole.exe WinXP_32 \\rockhopbuild\Hammer\HammerTools\DailyBuilds\LatestISO\40-00118-01.iso \\ryanadams2\HammerAuto WinToolsTestAutomation.dll WinToolsTestAutomation.Runner");
                    return -1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An exception has occured");
                Console.WriteLine(ex.ToString());
                return -1;
            }
        }
    }
}
