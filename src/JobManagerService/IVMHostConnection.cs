using System;
using System.Collections.Generic;
using System.Text;

namespace JobManagerService
{
    public interface IVMHostConnection
    {
        IVMConnection GetVMConnectionFromPath(string path);
        void Login();
        List<string> AllVMIdentifiers { get; }
    }
}
