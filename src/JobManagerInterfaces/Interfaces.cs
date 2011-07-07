using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace JobManagerInterfaces
{
    [Serializable]
    public class AutomationMessage : XMLSerializable 
    {
        public string Id { get; set; }
        //public string CorrelationId { get; set; }
        public string To { get; set; }
        public string From { get; set; }
        public AutomationMessageContent Content { get; set; }

        public AutomationMessage()
        {
            Id = Guid.NewGuid().ToString();
            From = Environment.MachineName;
        }

        public AutomationMessage(AutomationMessageContent content)
            : this()
        {
            this.Content = content;
        }

        public AutomationMessage(string to)
            : this()
        {
            this.To = to;
        }

        public AutomationMessage(string to, AutomationMessageContent content) 
            : this(to)
        {
            this.Content = content;
        }

        public AutomationMessage(string to, string forceID, AutomationMessageContent content)
            : this(to, content)
        {
            this.Id = forceID;
        }
    }

    [Serializable]
    [XmlInclude(typeof(JobCompleted))]
    [XmlInclude(typeof(SimpleRequest))]
    [XmlInclude(typeof(ErrorMessage))]
    [XmlInclude(typeof(JobReportReturn))]
    [XmlInclude(typeof(JobDeleteCommand))]
    [XmlInclude(typeof(JobCancelCommand))]
    [XmlInclude(typeof(JobCreate))]
    [XmlInclude(typeof(JobReturn))]
    [XmlInclude(typeof(VMRequestReturn))]
    [XmlInclude(typeof(LockVMCommand))]
    [XmlInclude(typeof(UnLockVMCommand))]
    public class AutomationMessageContent : XMLSerializable {}

    [Serializable]
    public class JobCreate : AutomationMessageContent
    {
        public Job j { get; set; }
        public JobCreate() { }
        public JobCreate(Job j)
        {
            this.j = j;
        }
    }

    [Serializable]
    public class JobReturn : AutomationMessageContent
    {
        public Job j { get; set; }
        public JobReturn() { }
        public JobReturn(Job j)
        {
            this.j = j;
        }
    }

    [Serializable]
    public class VMRequestReturn : AutomationMessageContent
    {
        public string[] VMPaths;
        public string[] LockedVMs;
        public VMRequestReturn() { }
        public VMRequestReturn(string[] vmPaths, string[] lockedVMs)
        {
            this.VMPaths = vmPaths;
            this.LockedVMs = lockedVMs;
        }
    }
    [Serializable]
    public class ExecutionResult
    {
        public bool Success;
        public bool RestartAfter;
        public List<string> Errors;
        public List<FileData> Attachments;
        public bool SnapshotOnShutdown;
        public string SnapshotName;
        public string SnapshotDesc;
        public bool CloneOnShutdown;

        public ExecutionResult()
        {
            Attachments = new List<FileData>();
            Errors = new List<string>();
            SnapshotOnShutdown = false;
            CloneOnShutdown = false;
            RestartAfter = false;
        }

        public ExecutionResult(IEnumerable<FileData> attachments)
            : this()
        {
            if (attachments != null)
            {
                Attachments.AddRange(attachments);
            }
        }

        public ExecutionResult(string error, IEnumerable<FileData> attachments) 
            : this(attachments)
        {
            Success = false;
            Errors.Add(error);
        }

        public ExecutionResult(string error, params FileData[] attachments)
            : this(attachments) 
        {
            Success = false;
            Errors.Add(error);
        }
    }

    [Serializable]
    public class JobResult : XMLSerializable
    {
        public bool Completed;
        public List<FileData> Logs;
        public List<ExecutionResult> ExecutionResults;

        public JobResult()
        {
            ExecutionResults = new List<ExecutionResult>();
            Logs = new List<FileData>();
        }

        public bool SnapshotOnShutdown
        {
            get
            {
                foreach (ExecutionResult er in ExecutionResults)
                {
                    if (er.SnapshotOnShutdown)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public string SnapshotName
        {
            get
            {
                foreach (ExecutionResult er in ExecutionResults)
                {
                    if (er.SnapshotOnShutdown)
                    {
                        return er.SnapshotName;
                    }
                }
                return null;
            }
        }

        public string SnapshotDesc
        {
            get
            {
                foreach (ExecutionResult er in ExecutionResults)
                {
                    if (er.SnapshotOnShutdown)
                    {
                        return er.SnapshotDesc;
                    }
                }
                return null;
            }
        }

        public bool CloneOnShutdown
        {
            get
            {
                foreach (ExecutionResult er in ExecutionResults)
                {
                    if (er.SnapshotOnShutdown)//we clone from this snapshot
                    {
                        return er.CloneOnShutdown;
                    }
                }
                return false;
            }
        }

        [XmlIgnore]
        public bool Success
        {
            get
            {
                bool success = true;
                foreach (ExecutionResult er in ExecutionResults)
                {
                    if (!er.Success)
                    {
                        success = false;
                        break;
                    }
                }
                return success;
            }
        }

        [XmlIgnore]
        public List<string> Errors
        {
            get
            {
                List<string> errors = new List<string>();
                foreach (ExecutionResult er in ExecutionResults)
                {
                    errors.AddRange(er.Errors);
                }
                return errors;
            }
        }

        [XmlIgnore]
        public List<FileData> Attachments
        {
            get
            {
                List<FileData> attachments = new List<FileData>();
                foreach (ExecutionResult er in ExecutionResults)
                {
                    attachments.AddRange(er.Attachments);
                }
                return attachments;
            }
        }
    }

    //public class JobError
    //{
    //    public string Error;
    //    public List<FileData> Attachments;

    //    public JobError()
    //    {
    //        Attachments = new List<FileData>();
    //    }

    //    public JobError(string simpleErrorMessage) : this()
    //    {
    //        Error = simpleErrorMessage;
    //    }
    //}

    public interface JobRunner
    {
        ExecutionResult Execute(JobClientInterface jci);
    }

    public interface StatusLogger
    {
        void LogString(string text);
    }

    public interface JobClientInterface
    {
        void LogString(string text);
        DriveInfo MountISO(string UNCPath);
        void StartupOnNextRun();
        Job RunningJob { get; }
        DirectoryInfo WorkingDir { get; }
        string GetPropertyValue(string key);
        void SetPropertyValue(string key, string value);
        bool ShutdownOnCompletion { get; set; }
    }

    [Serializable]
    public class ExecutablePackage
    {
        public string PackageXML;
        public string ContentDirectory;
        public SerializableDictionary<string, string> SubContentDirectories;
        public SerializableDictionary<string, string> Properties { get; set; }
        public string JobRunnerDLLName;
        public string JobRunnerClassName;

        public ExecutablePackage() { }

        public ExecutablePackage(string definedByXMLPath, string dir, string dllName, string className, SerializableDictionary<string, string> subContent, SerializableDictionary<string, string> properties)
        {
            this.PackageXML = definedByXMLPath;
            this.SubContentDirectories = subContent;
            this.ContentDirectory = dir;
            this.JobRunnerDLLName = dllName;
            this.JobRunnerClassName = className;
            this.Properties = properties;
        }
    }

    [Serializable]
    public class Job : XMLSerializable
    {
        public string JobID { get; set; }
        public DateTime StartDate { get; set; }
        public string Configuration { get; set; }
        public List<ExecutablePackage> Packages { get; set; }
        public SerializableDictionary<string, string> ISOs { get; set; }
        public string OriginalMessageID { get; set; }
        public string OriginalHost { get; set; }
        public SerializableDictionary<string, string> Properties { get; set; }
        public List<string> DependsOnJobIds { get; set; }
        public string ConfigurationXML { get; set; }
        public string JobXML { get; set; }
        public string SequenceXML { get; set; }
        public string BuildPath { get; set; }

        public Job() : base()
        {
            JobID = Guid.NewGuid().ToString();
            DependsOnJobIds = new List<string>();
            StartDate = DateTime.Now;
        }

        public Job(string buildPath, string configuration, SerializableDictionary<string, string> ISOs, List<ExecutablePackage> Packages, SerializableDictionary<string, string> Properties)
            : this()
        {
            this.BuildPath = buildPath;
            this.Configuration = configuration;
            this.ISOs = ISOs;
            this.Packages = Packages;
            this.OriginalHost = Environment.MachineName;
            this.Properties = Properties;
        }
    }

    public enum JobStates
    {
        Received,
        VMStarted,
        AutoStarted,
        AutoFinished,
        TakingSnapshot,
        WaitingForChildJobs,
        JobFinishedNotSent,
        JobFinishedSent
    }

    [Serializable]
    public class JobStatus : XMLSerializable
    {
        public JobStates _State { get; set; }
        public JobResult Result { get; set; }
        public DateTime LastStateChange { get; set; }
        public string VMPath { get; set; }

        public JobStatus()
        {
            State = JobStates.Received;
        }

        [XmlIgnore]
        public JobStates State
        {
            get
            {
                return _State;
            }
            set
            {
                if (value != _State)
                {
                    LastStateChange = DateTime.Now;
                    _State = value;
                }
            }
        }

        [XmlIgnore]
        public bool IsRunning
        {
            get { return State == JobStates.VMStarted || State == JobStates.AutoStarted || State == JobStates.TakingSnapshot || State == JobStates.WaitingForChildJobs || State == JobStates.AutoFinished; }
        }

        [XmlIgnore]
        public bool IsFinished
        {
            get { return State == JobStates.JobFinishedNotSent || State == JobStates.JobFinishedSent; }
        }

        public void ErrorOut(string errorMessage, FileInfo errorLog, IEnumerable<FileData> attachments)
        {
            State = JobStates.JobFinishedNotSent;
            if (Result == null)
            {
                Result = new JobResult();
            }
            Result.ExecutionResults.Add(new ExecutionResult(errorMessage, attachments));
            if (errorLog != null)
            {
                Result.Logs.Add(FileData.FromFile(errorLog));
            }
        }
    }

    [Serializable]
    public class JobReportReturn : AutomationMessageContent
    {
        public SerializableDictionary<Job, JobStatus> Jobs { get; set; }

        public JobReportReturn() : base() { }
        public JobReportReturn(Dictionary<Job, JobStatus> jobs)
            : this()
        {
            Jobs = new SerializableDictionary<Job, JobStatus>(jobs);
        }
    }

    [Serializable]
    public class JobCompleted : AutomationMessageContent
    {
        public Job Job { get; set; }
        public JobResult Result { get; set; }

        public JobCompleted() : base() { }
        public JobCompleted(Job j, JobResult jr) : base()
        {
            this.Job = j;
            this.Result = jr;
        }
    }

    public enum SimpleRequests
    {
        JobRequest,
        AllVMRequest,
        JobReport
    }

    //[Serializable]
    //public class JobRequest : AutomationMessageContent
    //{
    //    public JobRequest() : base() { }
    //}

    [Serializable]
    public class SimpleRequest : AutomationMessageContent
    {
        public SimpleRequests Request;

        public SimpleRequest() : base() { }
        public SimpleRequest(SimpleRequests request)
        {
            this.Request = request;
        }
    }

    [Serializable]
    public class JobDeleteCommand : AutomationMessageContent
    {
        public string JobID { get; set; }

        public JobDeleteCommand() : base() { }
        public JobDeleteCommand(string jobID)
            : base()
        {
            this.JobID = jobID;
        }
    }

    [Serializable]
    public class JobCancelCommand : AutomationMessageContent
    {
        public string JobID { get; set; }

        public JobCancelCommand() : base() { }
        public JobCancelCommand(string jobID)
            : base()
        {
            this.JobID = jobID;
        }
    }

    [Serializable]
    public class LockVMCommand : AutomationMessageContent
    {
        public string VMPath { get; set; }

        public LockVMCommand() : base() { }
        public LockVMCommand(string vmPath)
            : base()
        {
            this.VMPath = vmPath;
        }
    }

    [Serializable]
    public class UnLockVMCommand : AutomationMessageContent
    {
        public string VMPath { get; set; }

        public UnLockVMCommand() : base() { }
        public UnLockVMCommand(string vmPath)
            : base()
        {
            this.VMPath = vmPath;
        }
    }

    [Serializable]
    public class ErrorMessage : AutomationMessageContent
    {
        public const string ERROR_NO_JOB_FOUND = "Job Not Found";

        public string Error { get; set; }

        public ErrorMessage() : base() { }
        public ErrorMessage(string error)
            : this()
        {
            this.Error = error;
        }
    }

    public class DirectoryData
    {
        public string Name;
        public List<DirectoryData> Dirs;
        public List<FileData> Files;
        public DirectoryData()
        {
            Dirs = new List<DirectoryData>();
            Files = new List<FileData>();
        }

        public void DumpContentsToDir(DirectoryInfo di)
        {
            foreach (FileData fd in Files)
            {
                fd.WriteToFile(new FileInfo(Path.Combine(di.FullName, fd.Name)));
            }
            foreach (DirectoryData dd in Dirs)
            {
                DirectoryInfo sub = new DirectoryInfo(Path.Combine(di.FullName, dd.Name));
                if (!sub.Exists)
                {
                    sub.Create();
                }
                dd.DumpContentsToDir(sub);
            }
        }

        public static DirectoryData FromDirectory(DirectoryInfo di)
        {
            DirectoryData dd = new DirectoryData();
            dd.Name = di.Name;
            foreach (DirectoryInfo subDir in di.GetDirectories())
            {
                dd.Dirs.Add(DirectoryData.FromDirectory(subDir));
            }
            foreach (FileInfo file in di.GetFiles())
            {
                dd.Files.Add(FileData.FromFile(file));
            }
            return dd;
        }
    }

    public class FileData
    {
        public string Name;
        public byte[] Data;

        public void WriteToFile(FileInfo fi)
        {
            using (FileStream fs = new FileStream(fi.FullName, FileMode.Create, FileAccess.Write))
            {
                fs.Write(Data, 0, Data.Length);
            }
        }

        public void WriteToDir(DirectoryInfo di)
        {
            FileInfo fi = new FileInfo(Path.Combine(di.FullName, Name));
            WriteToFile(fi);
        }

        //renames the file if it already exists
        public void WriteToDirRename(DirectoryInfo di)
        {
            FileInfo fi = new FileInfo(Path.Combine(di.FullName, Name));
            int i = 0;
            while (fi.Exists)
            {
                fi = new FileInfo(Path.Combine(di.FullName, i + "_" + Name));
                i++;
            }
            WriteToFile(fi);
        }

        public static FileData FromFile(FileInfo fi)
        {
            FileData fd = new FileData();
            fd.Name = fi.Name;
            using (FileStream fs = new FileStream(fi.FullName, FileMode.Open, FileAccess.Read))
            {
                fd.Data = new byte[fs.Length];
                //TODO: what if size is greater than what int can handle
                fs.Read(fd.Data, 0, (int)fs.Length);
            }
            return fd;
        }
    }
}
