using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace JobManagerInterfaces
{
    public class JobCollectionSet
    {
        public List<JobManagerInterfaces.XML.JobCollection> jobCollections;
        public Dictionary<string, JobManagerInterfaces.XML.Job> jobMap;
        public Dictionary<string, JobManagerInterfaces.XML.ConfigurationType> configMap;
        public Dictionary<string, JobManagerInterfaces.XML.ISO> isoMap;
        public Dictionary<string, JobManagerInterfaces.XML.Sequence> sequenceMap;
        public Dictionary<string, JobManagerInterfaces.XML.Package> packageMap;

        private JobCollectionSet()
        {
            jobCollections = new List<XML.JobCollection>();
            jobMap = new Dictionary<string, XML.Job>();
            configMap = new Dictionary<string, XML.ConfigurationType>();
            isoMap = new Dictionary<string, XML.ISO>();
            sequenceMap = new Dictionary<string, XML.Sequence>();
            packageMap = new Dictionary<string, XML.Package>();
        }

        public JobCollectionSet(FileInfo jobCollectionFileInfo)
            : this()
        {
            ParseJobCollection(jobCollectionFileInfo);
        }

        public JobCollectionSet(DirectoryInfo rootDirInfo)
            : this()
        {
            foreach (FileInfo fi in rootDirInfo.GetFiles("*.xml"))
            {
                ParseJobCollection(fi);
            }
        }

        private void ParseJobCollection(FileInfo jobCollectionFile)
        {
            try
            {
                var jobCollectionXML = JobManagerInterfaces.XML.JobCollection.FromXML(jobCollectionFile.FullName);
                jobCollections.Add(jobCollectionXML);
                string rootDir = jobCollectionFile.Directory.FullName;
                foreach (var job in jobCollectionXML.Job)
                {
                    string jobXMLPath = job.JobPath;
                    var jobXML = JobManagerInterfaces.XML.Job.FromXML(Path.Combine(rootDir, jobXMLPath));
                    jobMap[jobXMLPath] = jobXML;
                    foreach (var configXMLPath in jobXML.Configurations)
                    {
                        var configXML = JobManagerInterfaces.XML.ConfigurationType.FromXML(Path.Combine(rootDir, configXMLPath));
                        configMap[configXMLPath] = configXML;
                    }
                    foreach (var iso in jobXML.ISOs)
                    {
                        var isoXMLPath = iso.Target;
                        var isoXML = JobManagerInterfaces.XML.ISO.FromXML(Path.Combine(rootDir, isoXMLPath));
                        isoMap[isoXMLPath] = isoXML;
                    }
                    var executableSeqXMLPath = jobXML.ExecutableSequence;
                    var executableSeqXML = JobManagerInterfaces.XML.Sequence.FromXML(Path.Combine(rootDir, executableSeqXMLPath));
                    sequenceMap[executableSeqXMLPath] = executableSeqXML;
                    foreach (var packageXMLPath in executableSeqXML.Package)
                    {
                        var packageXML = JobManagerInterfaces.XML.Package.FromXML(Path.Combine(rootDir, packageXMLPath));
                        packageMap[packageXMLPath] = packageXML;
                    }
                }
            }
            catch (Exception ex)
            {
                ex.Data.Add("JobCollectionFile", jobCollectionFile.FullName);
                throw ex;
            }
        }

        public List<Job> CreateJobs(string pathToBuildOutput)
        {
            var jobCollection = jobCollections[0];

            Dictionary<string, List<Job>> jobsToRun = new Dictionary<string, List<Job>>();

            foreach (var jobRef in jobCollection.Job)
            {
                List<string> definedBy = new List<string>();
                definedBy.Add(jobRef.JobPath);
                var job = jobMap[jobRef.JobPath];
                SerializableDictionary<string, string> isos = new SerializableDictionary<string, string>();
                foreach (var isoRef in job.ISOs)
                {
                    definedBy.Add(isoRef.Target);
                    var iso = isoMap[isoRef.Target];
                    string isoPath = iso.Path;
                    if (iso.PathType == JobManagerInterfaces.XML.ISOPathType.BuildRelative)
                    {
                        isoPath = Path.Combine(pathToBuildOutput, isoPath);
                    }

                    isos[isoRef.Key] = isoPath;
                }

                SerializableDictionary<string, string> properties = new SerializableDictionary<string, string>();

                foreach (var property in job.Properties)
                {
                    properties[property.Key] = property.Value;
                }

                List<ExecutablePackage> packages = new List<ExecutablePackage>();
                var sequence = sequenceMap[job.ExecutableSequence];
                definedBy.Add(job.ExecutableSequence);

                foreach (string packagePath in sequence.Package)
                {
                    definedBy.Add(packagePath);
                    var package = packageMap[packagePath];
                    string dir = package.MainDirectory.Path;
                    if (package.MainDirectory.PathType == JobManagerInterfaces.XML.PathTPathType.BuildRelative)
                    {
                        dir = Path.Combine(pathToBuildOutput, dir);
                    }

                    SerializableDictionary<string, string> subDirectories = new SerializableDictionary<string, string>();

                    foreach (var subDirectory in package.AdditionalSubDirectories)
                    {
                        string name = subDirectory.Name;
                        if (String.IsNullOrEmpty(name))
                        {
                            name = Path.GetDirectoryName(subDirectory.Path);
                        }
                        if (subDirectory.PathType == JobManagerInterfaces.XML.PathTPathType.BuildRelative)
                        {
                            subDirectories[name] = Path.Combine(pathToBuildOutput, subDirectory.Path);
                        }
                        else if (subDirectory.PathType == JobManagerInterfaces.XML.PathTPathType.Absolute)
                        {
                            subDirectories[name] = subDirectory.Path;
                        }
                    }

                    SerializableDictionary<string, string> packageProperties = new SerializableDictionary<string, string>();

                    foreach (var property in package.Properties)
                    {
                        packageProperties[property.Key] = property.Value;
                    }

                    packages.Add(new ExecutablePackage(packagePath, dir, package.DLLFileName, package.JobRunnerClassName, subDirectories, packageProperties));
                }

                foreach (var configPath in job.Configurations)
                {
                    SerializableDictionary<string, string> jobProperties = new SerializableDictionary<string, string>(properties);
                    var config = configMap[configPath];
                    foreach (var property in config.Properties)
                    {
                        if (!jobProperties.ContainsKey(property.Key))
                        {
                            jobProperties[property.Key] = property.Value;
                        }
                    }
                    Job j = new Job(pathToBuildOutput, config.VM, isos, packages, jobProperties);
                    j.ConfigurationXML = configPath;
                    j.JobXML = jobRef.JobPath;
                    j.SequenceXML = job.ExecutableSequence;
                    if (!jobsToRun.ContainsKey(jobRef.JobPath))
                    {
                        jobsToRun[jobRef.JobPath] = new List<Job>();
                    }
                    jobsToRun[jobRef.JobPath].Add(j);
                }
            }

            foreach (var jobRef in jobCollection.Job)
            {
                var baseJobPath = jobRef.DependsOn;
                if (baseJobPath != null)
                {
                    foreach (Job baseJob in jobsToRun[baseJobPath])
                    {
                        foreach (Job j in jobsToRun[jobRef.JobPath])
                        {
                            j.DependsOnJobIds.Add(baseJob.JobID);
                        }
                    }
                }
            }

            List<Job> jobs = new List<Job>();

            foreach (List<Job> jobList in jobsToRun.Values)
            {
                foreach (Job j in jobList)
                {
                    jobs.Add(j);
                }
            }
            return jobs;
        }
    }
}
