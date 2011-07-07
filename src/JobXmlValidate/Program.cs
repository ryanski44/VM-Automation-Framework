using System;
using System.Collections.Generic;
using System.Text;
using JobManagerInterfaces;
using System.IO;

namespace JobXmlValidate
{
    class Program
    {
        static int Main(string[] args)
        {
            DirectoryInfo curDir = new DirectoryInfo(Environment.CurrentDirectory);
            if (args.Length > 0)
            {
                string folderPath = args[0];
                if (Directory.Exists(folderPath))
                {
                    curDir = new DirectoryInfo(folderPath);
                }
            }

            bool success = true;
            try
            {
                JobCollectionSet set = new JobCollectionSet(curDir);
                foreach (var jobCollection in set.jobCollections)
                {
                    foreach (var jobRef in jobCollection.Job)
                    {
                        var job = set.jobMap[jobRef.JobPath];
                        var sequence = set.sequenceMap[job.ExecutableSequence];
                        foreach (var packagePath in sequence.Package)
                        {
                            var package = set.packageMap[packagePath];
                            foreach (string requiredISO in package.RequiredISOs)
                            {
                                bool foundISO = false;
                                foreach (var iso in job.ISOs)
                                {
                                    if (iso.Key == requiredISO)
                                    {
                                        foundISO = true;
                                        break;
                                    }
                                }

                                if (!foundISO)
                                {
                                    success = false;
                                    Console.WriteLine("Error: ISO \"" + requiredISO + "\", required by Package \"" + packagePath + "\", was not found in Job \"" + jobRef.JobPath + "\"");
                                }
                            }
                            foreach (string requiredPropertyKey in package.RequiredJobProperties)
                            {
                                bool foundProperty = false;
                                foreach (var property in job.Properties)
                                {
                                    if (property.Key == requiredPropertyKey)
                                    {
                                        foundProperty = true;
                                        break;
                                    }
                                }

                                if (!foundProperty)
                                {
                                    foundProperty = true;//assume it is found, we will set this to false if we don't find it
                                    foreach (string configPath in job.Configurations)
                                    {
                                        var config = set.configMap[configPath];
                                        bool foundPropertyInConfig = false;
                                        foreach (var property in config.Properties)
                                        {
                                            if (property.Key == requiredPropertyKey)
                                            {
                                                foundPropertyInConfig = true;
                                                break;
                                            }
                                        }
                                        if (!foundPropertyInConfig)
                                        {
                                            foundProperty = false;
                                            break;
                                        }
                                    }
                                }

                                if (!foundProperty)
                                {
                                    success = false;
                                    Console.WriteLine("Error: Property \"" + requiredPropertyKey + "\", required by Package \"" + packagePath + "\", was not found in Job \"" + jobRef.JobPath + "\"");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                success = false;
                Console.WriteLine(ex.ToString());
                foreach (string key in ex.Data.Keys)
                {
                    Console.Write(key + ":\t");
                    Console.WriteLine(ex.Data[key].ToString());
                }
            }

            if (success)
            {
                Console.WriteLine("No Errors");
                return 0;
            }
            else
            {
                return -1;
            }
        }
    }
}
