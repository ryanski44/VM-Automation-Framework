using System;
using System.Collections.Generic;
using System.Text;

namespace JobManagerService
{
    public class VSphereHostConnection : IVMHostConnection
    {
        public VSphereHostConnection() {}

        public IVMConnection GetVMConnectionFromPath(string path)
        {
            return new VSphereVMConnection(path);
        }

        public void Login()
        {
            if (VimHelper.CurrentSession == null)
            {
                VimHelper.Login();
            }
        }

        public List<string> AllVMIdentifiers
        {
            get
            {
                return VimHelper.AllVMPaths;
            }
        }
    }
}
