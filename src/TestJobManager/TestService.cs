using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using JobManagerService;
using System.Configuration;
//using System.IO;
using System.Net;
using Vim25Api;

namespace TestJobManager
{
    public class MyWebClient : System.Net.WebClient
    {
        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest wr = base.GetWebRequest(address);
            wr.PreAuthenticate = true;
            wr.Timeout = 10000;
            return wr;
        }
    }

    [TestFixture]
    public class TestService
    {
        [Test]
        public void TestRunCommand()
        {
            System.Configuration.Configuration config = ConfigurationManager.OpenExeConfiguration(
               ConfigurationUserLevel.None);

            config.AppSettings.Settings.Add("vixPath", @"C:\Program Files (x86)\VMware\VMware VIX");
            config.AppSettings.Settings.Add("vSphereHost", "vsphere-eng.quinton.com");
            config.AppSettings.Settings.Add("vSphereUser", "ENGDOM\\pyramiswebbuilder");
            config.AppSettings.Settings.Add("vSpherePass", "Gilbert93");

            config.Save(ConfigurationSaveMode.Modified);

            ConfigurationManager.RefreshSection("appSettings");

            Console.WriteLine(VIXAPI.RunCommand("list"));
        }

        //[Test]
        //public void TestUploadFile()
        //{
        //    System.Configuration.Configuration config = ConfigurationManager.OpenExeConfiguration(
        //                   ConfigurationUserLevel.None);

            //config.AppSettings.Settings.Add("vSphereHost", "10.4.2.78");
            //config.AppSettings.Settings.Add("vSphereUser", "root");
            //config.AppSettings.Settings.Add("vSpherePass", "Gilbert93");

        //    config.Save(ConfigurationSaveMode.Modified);

        //    ConfigurationManager.RefreshSection("appSettings");

        //    Console.WriteLine(VIXAPI.UploadFile(VIXAPI.DataStores.datastore2, new System.IO.System.IO.FileInfo(@"c:\temp.pdf")));
        //}


        public enum DataStores
        {
            datastore1,
            datastore2,
            datastore3,
            datastore4
        }

        private bool ValidateRemoteCertificate(
            object sender,
            System.Security.Cryptography.X509Certificates.X509Certificate certificate,
            System.Security.Cryptography.X509Certificates.X509Chain chain,
            System.Net.Security.SslPolicyErrors policyErrors)
        {

            // allow any old dodgy certificate...
            return true;
        }

        [Test, Explicit]
        public void TestRevertToSnapshot()
        {
            System.Configuration.Configuration config = ConfigurationManager.OpenExeConfiguration(
               ConfigurationUserLevel.None);
            config.AppSettings.Settings.Clear();
            config.AppSettings.Settings.Add("vixPath", @"C:\Program Files\VMware\VMware VIX");
            config.AppSettings.Settings.Add("vSphereHost", @"vsphere-eng.quinton.com");
            config.AppSettings.Settings.Add("vSphereUser", @"ENGDOM\pyramiswebbuilder");
            config.AppSettings.Settings.Add("vSpherePass", @"Gilbert93");
            config.AppSettings.Settings.Add("inbox", @"\\cmdstorage\Store2\VMAutomation\inbox");
            config.AppSettings.Settings.Add("outbox", @"\\cmdstorage\Store2\VMAutomation\outbox");
            config.AppSettings.Settings.Add("filedrop", @"\\cmdstorage\Store2\VMAutomation\drop");
            config.Save(ConfigurationSaveMode.Modified);

            ConfigurationManager.RefreshSection("appSettings");

            //WinXP_32, DataCenter/vm/Testing/VS-XP-PRO-SP3-IE6-x86, Auto, AutoXPProx86
            VSphereHostConnection conn = new VSphereHostConnection();
            VirtualMachine vm = new VirtualMachine("Auto", "AutoXPProx86", conn.GetVMConnectionFromPath("DataCenter/vm/Testing/VS-XP-PRO-SP3-IE6-x86"));
            vm.RevertToNamedSnapshot();
        }

        //[Test]
        //public void TestReconfigCD()
        //{
        //    VirtualMachine vm = new VirtualMachine("DataCenter/vm/HammerXP", "WINXPPROHAMMERT");

        //    vm.MountISO(JobManagerService.DataStores.datastore2, "40-00118-01.iso");
        //}

        [Test, Explicit]
        public void TestVMPowerOn()
        {
            VSphereHostConnection conn = new VSphereHostConnection();
            VirtualMachine vm = new VirtualMachine("Auto", "WINXPPROHAMMERT", conn.GetVMConnectionFromPath("DataCenter/vm/HammerXP"));
            vm.Start();
        }

        [Test, Explicit]
        public void TestUploadFile()
        {
            // hack to ignore bad certificates
            ServicePointManager.ServerCertificateValidationCallback += new System.Net.Security.RemoteCertificateValidationCallback(ValidateRemoteCertificate);

            var service = new VimService();
            service.Url = "https://vsphere-eng/sdk/vimService";
            service.CookieContainer = new CookieContainer();

            ServiceContent _sic = null;
            ManagedObjectReference _svcRef = new ManagedObjectReference();
            _svcRef.type = "ServiceInstance";
            _svcRef.Value = "ServiceInstance";

            _sic = service.RetrieveServiceContent(_svcRef);

            if (_sic.sessionManager != null)
            {
                service.Login(_sic.sessionManager, "ENGDOM\\pyramiswebbuilder", "Gilbert93", null);
            }

            Cookie cookie = service.CookieContainer.GetCookies(
                                      new Uri(service.Url))[0];
            String cookieString = cookie.ToString();

            DataStores datastore = DataStores.datastore4;
            System.IO.FileInfo fi = new System.IO.FileInfo(@"c:\temp.pdf");

            //MyWebClient wc = new MyWebClient();
            //wc.Credentials = new NetworkCredential("root", "Gilbert93");

            // wc.UploadString("https://10.4.2.78/folder/test.txt?dcPath=ha-datacenter&dsName=" + datastore.ToString(), "PUT", "Testing");

            //wc.UploadFile("https://10.4.2.78/folder/" + fi.Name + "?dcPath=ha-datacenter&dsName=" + datastore.ToString(), fi.FullName);

            // prepare the web page we will be asking for
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://vsphere-eng/folder/" + fi.Name + "?dcPath=DataCenter&dsName=" + datastore.ToString());
            request.Headers.Add(HttpRequestHeader.Cookie, cookieString);

            request.PreAuthenticate = true;
            request.UserAgent = "Upload Test";
            request.Method = "POST";

            request.Credentials = new NetworkCredential("ENGDOM\\pyramiswebbuilder", "Gilbert93");

            request.ContentType = "application/octet-stream";
            request.Accept = "100-continue";
            request.ContentLength = fi.Length;


            System.IO.Stream s = request.GetRequestStream();

            using (System.IO.FileStream fileStream = new System.IO.FileStream(fi.FullName, System.IO.FileMode.Open, System.IO.FileAccess.Read))
            {
                // used on each read operation
                byte[] buffer = new byte[1024];
                int len = 0;
                while ((len = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    s.Write(buffer, 0, len);
                }
            }

            s.Close();

            // execute the request
            WebResponse response = request.GetResponse();

            string result = String.Empty;

            using (System.IO.TextReader tr = new System.IO.StreamReader(response.GetResponseStream()))
            {
                result = tr.ReadToEnd();
            }

            Console.WriteLine(result);


            //service.Logout(_svcRef);
            
        }
    }
}
