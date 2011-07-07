using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.IO;
using System.Net;

namespace JobManagerService
{
    public static class VIXAPI
    {
        public static string RunCommand(params string[] args)
        {
            string command = Path.Combine(AppConfig.VIXPath, "vmrun.exe");

            string argumentString = "-h https://" + AppConfig.VSphereHost + "/sdk -u " + AppConfig.VSphereUser + " -p " + AppConfig.VSpherePass + " ";
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Contains(" "))
                {
                    args[i] = "\"" + args[i] + "\"";
                }
            }
            argumentString += String.Join(" ", args);
            
            ProcessStartInfo psi = new ProcessStartInfo(command, argumentString);
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            Process p = Process.Start(psi);
            p.WaitForExit();
            return p.StandardOutput.ReadToEnd();

            //Regex resultRegex = new Regex(@"^.*=([^=]*)$");
            //Match m = resultRegex.Match(result);
            //if (m.Success)
            //{
            //    if (m.Groups.Count > 1)
            //    {
            //        return m.Groups[1].Value.Trim();
            //    }
            //}
            //return String.Empty;
        }

        public enum DataStores
        {
            datastore1,
            datastore2,
            datastore3,
            datastore4
        }

        private static bool ValidateRemoteCertificate(
            object sender, 
            System.Security.Cryptography.X509Certificates.X509Certificate certificate,
            System.Security.Cryptography.X509Certificates.X509Chain chain, 
            System.Net.Security.SslPolicyErrors policyErrors)
        {

            // allow any old dodgy certificate...
            return true;
        }


        public static string UploadFile(DataStores datastore, FileInfo fi)
        {
            // used to build entire input
            StringBuilder sb = new StringBuilder();

            // used on each read operation
            byte[] buf = new byte[8192];

            // prepare the web page we will be asking for
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://" + AppConfig.VSphereHost + "/folder/" + fi.Name + "?dcPath=ha-datacenter&dsName=" + datastore.ToString());
            request.Credentials = new NetworkCredential(AppConfig.VSphereUser, AppConfig.VSpherePass);

            request.ContentType = "application/octet-stream";
            request.Method = "POST";
            request.Accept = "100-continue";
            request.ContentLength = fi.Length;

            // allows for validation of SSL conversations
            ServicePointManager.ServerCertificateValidationCallback += new System.Net.Security.RemoteCertificateValidationCallback(ValidateRemoteCertificate);
            
            Stream s = request.GetRequestStream();

            using (FileStream fileStream = new FileStream(fi.FullName, FileMode.Open, FileAccess.Read))
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

            using(TextReader tr = new StreamReader(response.GetResponseStream()))
            {
                result = tr.ReadToEnd();
            }

            return result;
        }
    }
}
