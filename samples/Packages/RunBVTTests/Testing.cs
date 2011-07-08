using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Text.RegularExpressions;
using JobManagerInterfaces;
using System.IO;
using System.Diagnostics;
using System.Xml;
using System.Xml.Xsl;
using System.Drawing.Imaging;
using System.Drawing;
using System.Windows.Forms;
using ProcessUtilities;
using System.Configuration;
using Microsoft.Win32;

namespace RunBVTTests
{
    public class Testing
    {
        public static ExecutionResult RunTests(JobClientInterface jci)
        {
            int testTimeoutMin = 30;
            string testTimeoutPropValue = jci.GetPropertyValue("testTimeoutMin");
            if (testTimeoutPropValue != null)
            {
                Int32.TryParse(testTimeoutPropValue, out testTimeoutMin);
            }
            TimeSpan testingTimeout = TimeSpan.FromMinutes(testTimeoutMin);

            DirectoryInfo workingDir = jci.WorkingDir;

            ExecutionResult jr = new ExecutionResult();
            jr.Success = true;
            string testDLLArg = String.Empty;
            foreach (string testDLLRaw in jci.GetPropertyValue("Tests").Split(';'))
            {
                string testDLL = testDLLRaw.Trim();
                FileInfo testDLLPath = new FileInfo(Path.Combine(workingDir.FullName, testDLL));
                testDLL = testDLLPath.Name;
                if (!testDLLPath.Exists)
                {
                    jr.Success = false;
                    jr.Errors.Add("Could not find test dll \"" + testDLLPath.FullName + "\". File does not exist.");
                    continue;
                }
                testDLLArg += "\"" + testDLLPath.FullName + "\" ";

                //look for any .config key/value pairs that we need to swap out
                Dictionary<string, string> configFileProperties = new Dictionary<string, string>();
                Dictionary<string, string> endPointAddresses = new Dictionary<string, string>();
                Dictionary<string, string> connectionStrings = new Dictionary<string, string>();
                string appConfigToken = testDLL + "_";
                string endpointToken = testDLL + "-endpoint-";
                string connStringToken = testDLL + "-connstring-";
                foreach (string key in jci.RunningJob.Properties.Keys)
                {
                    if (key.StartsWith(appConfigToken))
                    {
                        string actualKey = key.Substring(appConfigToken.Length, key.Length - appConfigToken.Length);
                        string value = jci.GetPropertyValue(key);
                        configFileProperties[actualKey] = value;
                    }
                    else if (key.StartsWith(endpointToken))
                    {
                        string endPointContract = key.Substring(endpointToken.Length, key.Length - endpointToken.Length);
                        string address = jci.GetPropertyValue(key);
                        endPointAddresses[endPointContract] = address;
                    }
                    else if (key.StartsWith(connStringToken))
                    {
                        string connStringName = key.Substring(connStringToken.Length, key.Length - connStringToken.Length);
                        string value = jci.GetPropertyValue(key);
                        connectionStrings[connStringName] = value;
                    }
                }
                FileInfo configFile = new FileInfo(testDLLPath.FullName + ".config");
                if (configFile.Exists && (configFileProperties.Count > 0 || endPointAddresses.Count > 0 || connectionStrings.Count > 0))
                {
                    var config = ConfigurationManager.OpenExeConfiguration(testDLLPath.FullName);
                    if (configFileProperties.Count > 0)
                    {
                        NameValueCollection currentProperties = new NameValueCollection();
                        foreach (KeyValueConfigurationElement ele in config.AppSettings.Settings)
                        {
                            currentProperties[ele.Key] = ele.Value;
                        }
                        foreach (string key in configFileProperties.Keys)
                        {
                            currentProperties[key] = configFileProperties[key];
                        }
                        config.AppSettings.Settings.Clear();
                        foreach (string key in currentProperties.Keys)
                        {
                            config.AppSettings.Settings.Add(key, currentProperties[key]);
                        }
                    }

                    if (endPointAddresses.Count > 0)
                    {
                        var serviceModelGroup = System.ServiceModel.Configuration.ServiceModelSectionGroup.GetSectionGroup(config);

                        foreach (System.ServiceModel.Configuration.ChannelEndpointElement endpointElement in serviceModelGroup.Client.Endpoints)
                        {
                            string contract = endpointElement.Contract;
                            if (endPointAddresses.ContainsKey(contract))
                            {
                                endpointElement.Address = new Uri(endPointAddresses[contract]);
                            }
                        }
                    }

                    if (connectionStrings.Count > 0)
                    {
                        var connStringSection = config.ConnectionStrings;
                        foreach (string connString in connectionStrings.Keys)
                        {
                            var connStringSettings = connStringSection.ConnectionStrings[connString];
                            if (connStringSettings != null)
                            {
                                connStringSettings.ConnectionString = connectionStrings[connString];
                            }
                        }
                    }

                    config.Save(ConfigurationSaveMode.Modified);
                }
            }
            string xmlOutputPath = Path.Combine(workingDir.FullName, "results.nur");
            string xsltPath = Path.Combine(workingDir.FullName, "HTMLDetail.xslt");

            List<string> categoriesToRun = new List<string>();
            List<string> categoriesToExlcude = new List<string>();
            string packageDefinedCategories = jci.GetPropertyValue("Package_NUnitCategories");
            string jobDefinedCategories = jci.GetPropertyValue("Job_NUnitCategories");
            if(IsTrue(jci.GetPropertyValue("ExcludeJobCategories")))
            {
                jobDefinedCategories = null;
            }
            string configDefinedCategories = jci.GetPropertyValue("Config_NUnitCategories");
            if (IsTrue(jci.GetPropertyValue("ExcludeConfigurationCategories")))
            {
                configDefinedCategories = null;
            }
            string excludeCategories = jci.GetPropertyValue("Package_Exclude_NUnitCategories");
            foreach (string categoriesStr in new string[] { packageDefinedCategories, jobDefinedCategories, configDefinedCategories })
            {
                if (categoriesStr != null)
                {
                    string[] categories = categoriesStr.Split(';');
                    foreach (string category in categories)
                    {
                        categoriesToRun.Add(category);
                    }
                }
            }
            if (excludeCategories != null)
            {
                string[] categories = excludeCategories.Split(';');
                foreach (string category in categories)
                {
                    categoriesToExlcude.Add(category);
                }
            }

            string nUnitConsoleArgs = testDLLArg + " /xml=\"" + xmlOutputPath + "\" /noshadow";
            if (categoriesToRun.Count > 0)
            {
                nUnitConsoleArgs += " /include:" + String.Join(",", categoriesToRun.ToArray());
            }
            if (categoriesToExlcude.Count > 0)
            {
                nUnitConsoleArgs += " /exclude:" + String.Join(",", categoriesToExlcude.ToArray());
            }

            string nunitExe = Path.Combine(workingDir.FullName, "nunit-console-x86.exe");
            string nunitAgentExe = Path.Combine(workingDir.FullName, "nunit-agent-x86.exe");

            //put in exception for program in windows firewall
            //netsh.exe firewall set allowedprogram program = "path\to\program" name = "appName" mode = ENABLE

            UtilityBackgroundProcess netshProc = new UtilityBackgroundProcess("netsh.exe");
            netshProc.DebugMessageSent += new DebugMessageHandler(delegate(object sender, string message) { jci.LogString(message); });
            netshProc.Run("firewall set allowedprogram program = \"" + nunitAgentExe + "\" name = \"NUnit Agent Proc\" mode = ENABLE");
            netshProc.WaitForExit();

            jci.LogString("Running Unit Tests from \"" + testDLLArg + "\" and outputing to \"" + xmlOutputPath + "\"");
            int retryCount = 0;
            UtilityBackgroundProcess p = null;
            while (retryCount < 2)
            {
                p = new UtilityBackgroundProcess(nunitExe);
                p.DebugMessageSent += new DebugMessageHandler(delegate(object sender, string message) { jci.LogString(message); });
                p.Run(nUnitConsoleArgs);
                if (!p.WaitForExit(testingTimeout))
                {
                    jci.LogString("nunit-console-x86.exe ran for longer than the timeout of " + testingTimeout.TotalMinutes + " minutes and was stopped.");
                }
                if (p.ExitCode < 0 && p.StandardError.Contains("Unable to locate fixture "))
                {
                    jci.LogString("nunit failed with \"Unable to locate fixture\", runing nunit again...");
                    retryCount++;
                }
                else
                {
                    break;
                }
            }
            string nunitOutput = p.StandardOutput;
            FileData nunitConsoleOutput = new FileData();
            nunitConsoleOutput.Name = "nunit.log";
            nunitConsoleOutput.Data = Encoding.UTF8.GetBytes(nunitOutput);
            jr.Attachments.Add(nunitConsoleOutput);

            string nunitErr = p.StandardError;
            FileData nunitErrOutput = new FileData();
            nunitErrOutput.Name = "nunit.err.log";
            nunitErrOutput.Data = Encoding.UTF8.GetBytes(nunitErr);
            jr.Attachments.Add(nunitErrOutput);

            Regex regex = new Regex(@"Tests run:\s+\d+,\s+Errors:\s+(\d+),\s+Failures:\s+(\d+)");
            Match m = regex.Match(nunitOutput);

            bool nunitPassed = m.Success && m.Groups.Count > 2 && m.Groups[1].Value == "0" && m.Groups[2].Value == "0";

            jci.LogString("Done Running Unit Tests. Result: " + (nunitPassed ? "Success" : "Failure"));

            if (File.Exists(xmlOutputPath))
            {
                using (Stream resultsSummary = TransformXML(xmlOutputPath, Path.Combine(workingDir.FullName, xsltPath)))
                {
                    using (TextReader tr = new StreamReader(resultsSummary))
                    {
                        List<FileData> attachments = new List<FileData>();
                        attachments.Add(FileData.FromFile(new FileInfo(xmlOutputPath)));
                        FileData resultFileData = new FileData();
                        resultFileData.Name = "result.html";
                        resultFileData.Data = new byte[resultsSummary.Length];
                        resultsSummary.Read(resultFileData.Data, 0, (int)resultsSummary.Length);

                        attachments.Add(resultFileData);

                        jr.Attachments.AddRange(attachments);
                        string treatFailedTestsAsPackageFailure = jci.GetPropertyValue("TreatFailedTestsAsPackageFailure");
                        if (!nunitPassed && treatFailedTestsAsPackageFailure != null && treatFailedTestsAsPackageFailure.ToLower() == "true")
                        {
                            jr.Errors.Add("Nunit tests did not pass!");
                            jr.Success = false;
                        }
                    }
                }
            }
            else
            {
                string nunitErrorPath = Path.Combine(workingDir.FullName, "nunit_error.png");
                TakeScreenShot(nunitErrorPath);
                jr.Success = false;
                jr.Attachments.Add(FileData.FromFile(new FileInfo(nunitErrorPath)));
                jr.Errors.Add("Nunit tests did not run! See attached screenshot and log!");
            }

            string shutdownStr = jci.GetPropertyValue("ShutdownAfterTests");
            if (shutdownStr != null && shutdownStr.ToLower() == "true")
            {
                jci.ShutdownOnCompletion = true;
            }

            return jr;
        }

        private static bool IsTrue(string propertyString)
        {
            return propertyString != null && propertyString.ToLower() == "true";
        }

        public static Stream TransformXML(string xmlFilePath, string xsltFilePath)
        {
            XslCompiledTransform transform = new XslCompiledTransform();

            // Load the XSL stylesheet.
            transform.Load(xsltFilePath);

            // Load the XML document
            XmlDocument doc = new XmlDocument();
            doc.Load(xmlFilePath);

            //Prepare a memory stream for the output
            Stream stream = new MemoryStream();

            // Transform
            transform.Transform(doc, null, stream);

            //reset the stream position to the beginning and return the memory stream
            stream.Position = 0;
            return stream;
        }

        public static void TakeScreenShot(string destPNGFileName)
        {
            TakeScreenShot(destPNGFileName, null);
        }

        public static void TakeScreenShot(string destPNGFileName, Rectangle bounds)
        {
            Rectangle realBounds = new Rectangle((int)bounds.Left, (int)bounds.Top, (int)bounds.Width, (int)bounds.Height);
            TakeScreenShot(destPNGFileName, realBounds);
        }

        public static void TakeScreenShot(string destPNGFileName, Rectangle? bounds)
        {
            Bitmap bmpScreenshot = null;
            if (bounds == null)
            {
                bmpScreenshot = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height, PixelFormat.Format32bppArgb);
            }
            else
            {
                bmpScreenshot = new Bitmap(bounds.Value.Width, bounds.Value.Height, PixelFormat.Format32bppArgb);
            }

            // Create a graphics object from the bitmap
            Graphics gfxScreenshot = Graphics.FromImage(bmpScreenshot);

            if (bounds == null)
            {
                // Take the screenshot from the upper left corner to the right bottom corner
                gfxScreenshot.CopyFromScreen(Screen.PrimaryScreen.Bounds.X, Screen.PrimaryScreen.Bounds.Y, 0, 0, Screen.PrimaryScreen.Bounds.Size, CopyPixelOperation.SourceCopy);
            }
            else
            {
                Rectangle realBounds = bounds.Value;
                gfxScreenshot.CopyFromScreen(realBounds.Left, realBounds.Top, 0, 0, new System.Drawing.Size(realBounds.Width, realBounds.Height), CopyPixelOperation.SourceCopy);
            }

            // Save the screenshot to the specified path that the user has chosen
            bmpScreenshot.Save(destPNGFileName, ImageFormat.Png);
        }

        public static string ProgramFilesx86
        {
            get
            {
                if (ArchitectureIs64Bit)
                {
                    return Environment.GetEnvironmentVariable("ProgramFiles(x86)");
                }
                else
                {
                    return Environment.GetEnvironmentVariable("ProgramFiles");
                }
            }
        }

        public static bool ArchitectureIs64Bit
        {
            get
            {
                return !String.IsNullOrEmpty(Environment.GetEnvironmentVariable("ProgramFiles(x86)"));
            }
        }
    }
}
