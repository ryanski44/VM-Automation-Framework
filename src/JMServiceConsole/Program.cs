using System;
using System.Collections.Generic;
using System.Text;
using JobManagerService;

namespace JMServiceConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                MockService service = new MockService();
                service.MockStart(args);
                Console.WriteLine("Service has started");
                Console.WriteLine("Press any key to exit");
                Console.Read();
                service.MockStop();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }

    public class MockService : JobManager
    {
        public MockService() : base() { }

        public void MockStart(string[] args)
        {
            base.OnStart(args);
        }

        public void MockStop()
        {
            base.OnStop();
        }
    }
}
