using System;
using System.Collections.Generic;
using System.Text;

namespace JobManagerClient
{
    public interface ISystem
    {
        void Shutdown(bool restart);
        void UpdateStatus(string text, bool log);
        void UpdateStatus(string text);
    }
}
