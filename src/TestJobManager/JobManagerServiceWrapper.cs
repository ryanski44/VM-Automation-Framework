using System;
using System.Collections.Generic;
using System.Text;
using JobManagerService;

namespace TestJobManager
{
    public class JobManagerServiceWrapper : JobManager
    {
        public JobManagerServiceWrapper() : base() 
        {
            vmHost = new MockVMHostConnection();
        }

        public void EmulateServiceStart(string[] args)
        {
            base.OnStart(args);
        }

        public void EmulateServiceStop()
        {
            base.OnStop();
        }

        public MockVMHostConnection MockVMHost
        {
            get { return vmHost as MockVMHostConnection; }
        }
    }

    public class MockVMHostConnection : IVMHostConnection
    {
        private List<MockVMConnection> vms;
        public MockVMHostConnection()
        {
            vms = new List<MockVMConnection>();
        }

        public IVMConnection GetVMConnectionFromPath(string path)
        {
            foreach (MockVMConnection vm in vms)
            {
                if (vm._Identifier == path)
                {
                    return vm;
                }
            }
            return null;
        }

        public void Login() { }

        public List<string> AllVMIdentifiers
        {
            get
            {
                //TODO
                return new List<string>();
            }
        }

        public MockVMConnection AddMockVM(string vmName)
        {
            MockVMConnection mockVM = new MockVMConnection();
            mockVM.VMName = vmName;
            vms.Add(mockVM);
            return mockVM;
        }
    }

    public class MockVMConnection : IVMConnection
    {
        private List<VMAction> history;

        public string _VMName;
        private bool _IsStarted;
        private string _ComputeResourceName;
        private bool _HasValidConnection;
        public string _Identifier;
        public MockVMConnection()
        {
            _VMName = "Mock VM";
            _IsStarted = false;
            _ComputeResourceName = "Mock Resource";
            _HasValidConnection = true;
            _Identifier = Guid.NewGuid().ToString();
            history = new List<VMAction>();
        }

        public string VMName
        {
            get
            {
                history.Add(new VMAction(VMActionType.GetVMName));
                return _VMName;
            }
            set { _VMName = value; }
        }

        public IEnumerable<VMAction> History
        {
            get { return history; }
        }

        public bool IsStarted
        {
            get
            {
                history.Add(new VMAction(VMActionType.GetIsStarted));
                return _IsStarted;
            }
            set { _IsStarted = value; }
        }

        public string ComputeResourceName
        {
            get
            {
                history.Add(new VMAction(VMActionType.GetComputeResourceName));
                return _ComputeResourceName;
            }
            set { _ComputeResourceName = value; }
        }

        public void Start()
        {
            history.Add(new VMAction(VMActionType.Start));
            IsStarted = true;
        }

        public void RevertToCurrentSnapshot()
        {
            history.Add(new VMAction(VMActionType.RevertToCurrentSnapshot));
        }

        public void RevertToNamedSnapshot(string snapshotName)
        {
            history.Add(new VMAction(VMActionType.RevertToNamedSnapshot, snapshotName));
        }

        public void TakeSnapshot(string snapShotName, string desc)
        {
            history.Add(new VMAction(VMActionType.TakeSnapshot, snapShotName, desc));
        }

        public IVMConnection CreateLinkedClone(string snapshotName, string linkedVMName)
        {
            history.Add(new VMAction(VMActionType.CreateLinkedClone, snapshotName, linkedVMName));
            return new MockVMConnection();
        }

        public bool HasValidConnection
        {
            get
            {
                history.Add(new VMAction(VMActionType.GetHasValidConnection));
                return _HasValidConnection;
            }
            set { _HasValidConnection = value; }
        }

        public string Identifier
        {
            get
            {
                history.Add(new VMAction(VMActionType.GetIdentifier));
                return _Identifier;
            }
            set { _Identifier = value; }
        }
    }

    public struct VMAction
    {
        public DateTime Time;
        public VMActionType Action;
        public string[] Params;
        public VMAction(VMActionType action)
        {
            this.Time = DateTime.Now;
            this.Action = action;
            this.Params = new string[0];
        }
        public VMAction(VMActionType action, params string[] parameters)
        {
            this.Time = DateTime.Now;
            this.Action = action;
            this.Params = parameters;
        }
    }

    public enum VMActionType
    {
        Start,
        RevertToCurrentSnapshot,
        RevertToNamedSnapshot,
        TakeSnapshot,
        CreateLinkedClone,
        GetHasValidConnection,
        GetIdentifier,
        GetVMName,
        GetIsStarted,
        GetComputeResourceName
    }
}
