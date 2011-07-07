using System;
using System.Collections.Generic;
using System.Text;
using Vim25Api;

namespace JobManagerService
{
    public interface IVMConnection
    {
        string VMName { get; }
        bool IsStarted { get; }
        string ComputeResourceName { get; }
        void Start();
        void RevertToCurrentSnapshot();
        void RevertToNamedSnapshot(string snapshotName);
        void TakeSnapshot(string snapShotName, string desc);
        IVMConnection CreateLinkedClone(string snapshotName, string linkedVMName);
        bool HasValidConnection { get; }
        string Identifier { get; }
    }

    public class VSphereVMConnection : IVMConnection
    {
        private string path;
        private ManagedObjectReference _vimRef;
        private string cachedComputeResource;
        private string cachedVMName;

        public VSphereVMConnection(string path)
        {
            this.path = path;
        }

        public VSphereVMConnection(ManagedObjectReference mob)
        {
            this._vimRef = mob;
        }

        public string Identifier
        {
            get { return path; }
        }

        private ManagedObjectReference MOB
        {
            get
            {
                if (VimHelper.CurrentSession == null)
                {
                    VimHelper.Login();
                    _vimRef = null;
                }
                if (_vimRef == null)
                {
                    _vimRef = VimHelper.ServiceInstance.FindByInventoryPath(VimHelper.ServiceContent.searchIndex, path);
                }
                return _vimRef;
            }
        }

        public string VMName
        {
            get
            {
                if (cachedVMName == null)
                {
                    VirtualMachineConfigInfo config = VimHelper.GetDynamicProperty<VirtualMachineConfigInfo>(MOB, "config");
                    cachedVMName = config.name;
                }
                return cachedVMName;
            }
        }

        public bool IsStarted
        {
            get
            {
                VirtualMachineRuntimeInfo vmri = VimHelper.GetDynamicProperty<VirtualMachineRuntimeInfo>(MOB, "runtime");
                return vmri.powerState == VirtualMachinePowerState.poweredOn;
            }
        }

        public string ComputeResourceName
        {
            get
            {
                if (cachedComputeResource == null)
                {
                    ManagedObjectReference resourcePoolMOB = VimHelper.GetDynamicProperty<ManagedObjectReference>(MOB, "resourcePool");
                    ManagedObjectReference computeResourceMOB = VimHelper.GetDynamicProperty<ManagedObjectReference>(resourcePoolMOB, "owner");
                    cachedComputeResource = VimHelper.GetDynamicProperty<string>(computeResourceMOB, "name");
                }
                return cachedComputeResource;
            }
        }

        public void Start()
        {
            //VIXAPI.RunCommand("start", path);
            VimHelper.ServiceInstance.PowerOnVM_Task(MOB, null);
        }

        public void RevertToCurrentSnapshot()
        {
            ManagedObjectReference task = VimHelper.ServiceInstance.RevertToCurrentSnapshot_Task(MOB, null, false, false);
            while (true)
            {
                TaskInfo info = VimHelper.GetDynamicProperty<TaskInfo>(task, "info");
                if (info.state == TaskInfoState.success || info.state == TaskInfoState.error)
                {
                    break;
                }
                System.Threading.Thread.Sleep(500);
            }
        }

        public void RevertToNamedSnapshot(string snapshotName)
        {
            var snapshot = VimHelper.GetDynamicProperty<VirtualMachineSnapshotInfo>(MOB, "snapshot");
            if (snapshot != null)
            {
                var snapshotMOB = FindSnapshotByName(snapshot.rootSnapshotList, snapshotName);
                if (snapshotMOB != null)
                {
                    ManagedObjectReference task = VimHelper.ServiceInstance.RevertToSnapshot_Task(snapshotMOB, null, false, false);
                    while (true)
                    {
                        TaskInfo info = VimHelper.GetDynamicProperty<TaskInfo>(task, "info");
                        if (info.state == TaskInfoState.success || info.state == TaskInfoState.error)
                        {
                            break;
                        }
                        System.Threading.Thread.Sleep(500);
                    }
                }
                else
                {
                    throw new NullReferenceException("Could not find the snapshot named '" + snapshotName + "' for VM '" + path + "'");
                }
            }
            else
            {
                throw new NullReferenceException("Could not find the 'snapshot' property for this VM, or the property was null. VM: " + path);
            }
        }

        private ManagedObjectReference FindSnapshotByName(VirtualMachineSnapshotTree[] tree, string name)
        {
            ManagedObjectReference result = null;
            foreach (VirtualMachineSnapshotTree snapshotNode in tree)
            {
                if (snapshotNode.childSnapshotList != null && snapshotNode.childSnapshotList.Length > 0)
                {
                    result = FindSnapshotByName(snapshotNode.childSnapshotList, name);
                }
                if (result != null)
                {
                    break;
                }
                else//if the result wasn't found farther down in the tree, then we check this snapshot node to see if it matches the name
                {
                    if (snapshotNode.name == name)
                    {
                        result = snapshotNode.snapshot;
                        break;
                    }
                }
            }
            return result;
        }

        public void TakeSnapshot(string snapShotName, string desc)
        {
            ManagedObjectReference task = VimHelper.ServiceInstance.CreateSnapshot_Task(MOB, snapShotName, desc, false, false);
            while (true)
            {
                TaskInfo info = VimHelper.GetDynamicProperty<TaskInfo>(task, "info");
                if (info.state == TaskInfoState.success || info.state == TaskInfoState.error)
                {
                    break;
                }
                System.Threading.Thread.Sleep(500);
            }
        }

        public IVMConnection CreateLinkedClone(string snapshotName, string linkedVMName)
        {
            var snapshot = VimHelper.GetDynamicProperty<VirtualMachineSnapshotInfo>(MOB, "snapshot");
            if (snapshot != null)
            {
                var snapshotMOB = FindSnapshotByName(snapshot.rootSnapshotList, snapshotName);
                if (snapshotMOB != null)
                {
                    var relSpec = new VirtualMachineRelocateSpec();
                    relSpec.diskMoveType = "createNewChildDiskBacking";
                    var cloneSpec = new VirtualMachineCloneSpec();
                    cloneSpec.powerOn = false;
                    cloneSpec.template = false;
                    cloneSpec.location = relSpec;
                    cloneSpec.snapshot = snapshotMOB;
                    ManagedObjectReference folder = VimHelper.GetDynamicProperty<ManagedObjectReference>(MOB, "parent");
                    ManagedObjectReference task = VimHelper.ServiceInstance.CloneVM_Task(MOB, folder, linkedVMName, cloneSpec);
                    while (true)
                    {
                        TaskInfo info = VimHelper.GetDynamicProperty<TaskInfo>(task, "info");
                        if (info.state == TaskInfoState.success)
                        {
                            return new VSphereVMConnection(info.result as ManagedObjectReference);
                        }
                        else if (info.state == TaskInfoState.error)
                        {
                            throw new Exception(info.error.localizedMessage);
                        }
                        System.Threading.Thread.Sleep(500);
                    }
                }
            }
            return null;
        }

        private ManagedObjectReference getDataStore(DataStores datastore)
        {
            ManagedObjectReference[] datastores = VimHelper.GetDynamicProperty<ManagedObjectReference[]>(VimHelper.DataCenterMOB, "datastore");

            for (int i = 0; i < datastores.Length; i++)
            {
                ManagedObjectReference curDatastore = datastores[i];
                string name = VimHelper.GetDynamicProperty<string>(curDatastore, "name");
                if (name == datastore.ToString())
                {
                    return curDatastore;
                }
            }
            return null;
        }

        public bool HasValidConnection
        {
            get
            {
                return _vimRef != null;
            }
        }
    }

    public class VirtualMachine
    {
        private string hostName;
        private string snapshot;
        private IVMConnection conn;

        public VirtualMachine(string snapshot, string hostName, IVMConnection underlyingConnection)
        {
            this.hostName = hostName;
            this.snapshot = snapshot;
            this.conn = underlyingConnection;
        }

        public bool IsSameHost(string inputHostName)
        {
            return hostName.ToLower().Equals(inputHostName.ToLower());
        }

        public string HostName
        {
            get { return hostName; }
        }

        public string SnapshotName
        {
            get { return snapshot; }
        }

        public string VMName
        {
            get
            {
                return conn.VMName;
            }
        }

        public string Identifier
        {
            get { return conn.Identifier; }
        }

        public bool IsStarted
        {
            get 
            {
                return conn.IsStarted;
            }
        }

        public string ComputeResourceName
        {
            get
            {
                return conn.ComputeResourceName;
            }
        }

        public void Start()
        {
            conn.Start();
        }

        public void RevertToCurrentSnapshot()
        {
            conn.RevertToCurrentSnapshot();
        }

        public void RevertToNamedSnapshot()
        {
            RevertToNamedSnapshot(snapshot);
        }

        public void RevertToNamedSnapshot(string snapshotName)
        {
            conn.RevertToNamedSnapshot(snapshotName);
        }

        public void TakeSnapshot(string snapShotName, string desc)
        {
            conn.TakeSnapshot(snapShotName, desc);
        }

        public IVMConnection CreateLinkedClone(string snapshotName, string linkedVMName)
        {
            return conn.CreateLinkedClone(snapshotName, linkedVMName);
        }

        public bool HasValidConnection { get { return conn.HasValidConnection; } }
    }
}
