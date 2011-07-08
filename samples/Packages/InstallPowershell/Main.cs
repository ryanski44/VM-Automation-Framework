using System;
using System.Collections.Generic;
using System.Text;
using JobManagerInterfaces;
using System.IO;
using ProcessUtilities;
using System.Runtime.InteropServices;

namespace InstallPowershell
{
    public class Main : JobRunner
    {
        public ExecutionResult Execute(JobClientInterface jci)
        {
            ExecutionResult er = new ExecutionResult();
            er.Success = true;

            if (!Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell")))
            {
                int major = Environment.OSVersion.Version.Major;
                int minor = Environment.OSVersion.Version.Minor;

                string installerToRun = null;

                WINDOWS_OS? os = GetOS();

                if (os != null)
                {
                    switch (os.Value)
                    {
                        case WINDOWS_OS.WindowsXP:
                            if (ArchitectureIs64Bit)
                            {
                                installerToRun = "PS_WindowsServer2003.WindowsXP-KB926139-v2-x64-ENU.exe";
                            }
                            else
                            {
                                installerToRun = "PS_WindowsXP-KB926139-v2-x86-ENU.exe";
                            }
                            break;
                        case WINDOWS_OS.WindowsVista:
                            if (ArchitectureIs64Bit)
                            {
                                installerToRun = "PS_Vista_Windows6.0-KB928439-x64.msu";
                            }
                            else
                            {
                                installerToRun = "PS_Vista_Windows6.0-KB928439-x86.msu";
                            }
                            break;
                        case WINDOWS_OS.Windows7:
                            //no need
                            break;
                        case WINDOWS_OS.WindowsServer2003:
                            if (ArchitectureIs64Bit)
                            {
                                installerToRun = "PS_WindowsServer2003.WindowsXP-KB926139-v2-x64-ENU.exe";
                            }
                            else
                            {
                                installerToRun = "PS_WindowsServer2003-KB926139-v2-x86-ENU.exe";
                            }
                            break;
                        case WINDOWS_OS.WindowsServer2008:
                            jci.LogString("Installing PowerShell for Server 2008");

                            string pkgmgrPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "pkgmgr.exe");
                            string pkgmgr64Path = Path.Combine(Environment.ExpandEnvironmentVariables("%windir%\\sysnative"), "pkgmgr.exe");
                            if (File.Exists(pkgmgr64Path))
                            {
                                pkgmgrPath = pkgmgr64Path;
                            }

                            UtilityBackgroundProcess proc = new UtilityBackgroundProcess(pkgmgrPath);
                            proc.DebugMessageSent += new DebugMessageHandler(delegate(object sender, string message) { jci.LogString(message); });
                            if (proc.Run("/iu:MicrosoftWindowsPowerShell"))
                            {
                                if (proc.WaitForExit(TimeSpan.FromMinutes(45)))
                                {
                                    int exitCode = proc.ExitCode.Value;
                                    if (exitCode != 0)
                                    {
                                        er.Success = false;
                                        er.Errors.Add("Exit Code for pkmgr.exe was non-zero: " + exitCode);
                                    }
                                }
                                else
                                {
                                    er.Success = false;
                                    er.Errors.Add("pkmgr.exe timedout and was terminated.");
                                }
                            }
                            else
                            {
                                er.Success = false;
                                er.Errors.Add("pkmgr.exe could not be started, or terminated immediatly");
                            }
                            jci.LogString("Done Installing PowerShell for Server 2008");
                            break;
                        case WINDOWS_OS.WindowsServer2008R2:
                            //no need
                            break;
                    }
                }

                if (installerToRun != null)
                {
                    installerToRun = Path.Combine(jci.WorkingDir.FullName, installerToRun);
                    UtilityBackgroundProcess proc;
                    string args = String.Empty;
                    if (installerToRun.EndsWith("msu"))
                    {
                        proc = new UtilityBackgroundProcess("wusa");
                        args = "\"" + installerToRun + "\" /quiet";
                    }
                    else
                    {
                        proc = new UtilityBackgroundProcess(installerToRun);
                        args = "/quiet";
                    }
                    proc.DebugMessageSent += new DebugMessageHandler(delegate(object sender, string message) { jci.LogString(message); });
                    proc.Run(args);
                    if (proc.WaitForExit(TimeSpan.FromMinutes(30)))
                    {
                        if (proc.ExitCode != null)
                        {
                            if (proc.ExitCode.Value == 0)
                            {
                                proc.WaitForSubProcessToExit();
                            }
                            else
                            {
                                er.Success = false;
                                string message = "The process " + proc.ProgramEXE + " returned a non-zero exit code: " + proc.ExitCode.Value;
                                er.Errors.Add(message);
                                jci.LogString(message);
                            }
                        }
                        else
                        {
                            er.Success = false;
                            string message = "The process " + proc.ProgramEXE + " did not return an exit code.";
                            er.Errors.Add(message);
                            jci.LogString(message);
                        }
                    }
                    else
                    {
                        er.Success = false;
                        string message = "The process " + proc.ProgramEXE + " timed out after 30 minutes and was terminated.";
                        er.Errors.Add(message);
                        jci.LogString(message);
                    }
                }
            }

            return er;
        }

        public static bool ArchitectureIs64Bit
        {
            get
            {
                return !String.IsNullOrEmpty(Environment.GetEnvironmentVariable("ProgramFiles(x86)"));
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct OSVERSIONINFOEX
        {
            public int dwOSVersionInfoSize;
            public int dwMajorVersion;
            public int dwMinorVersion;
            public int dwBuildNumber;
            public int dwPlatformId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szCSDVersion;
            public short wServicePackMajor;
            public short wServicePackMinor;
            public short wSuiteMask;
            public byte wProductType;
            public byte wReserved;
        }

        [DllImport("kernel32.dll")]
        private static extern bool GetVersionEx(ref OSVERSIONINFOEX osVersionInfo);

        private const int VER_NT_WORKSTATION = 1;
        private const int VER_NT_DOMAIN_CONTROLLER = 2;
        private const int VER_NT_SERVER = 3;

        public enum WINDOWS_OS
        {
            Windows2000,
            Windows2000Server,
            WindowsXP,
            WindowsServer2003,
            WindowsServer2008,
            WindowsServer2008R2,
            WindowsVista,
            Windows7
        }

        public static WINDOWS_OS? GetOS()
        {
            OSVERSIONINFOEX osVersionInfo = new OSVERSIONINFOEX();
            OperatingSystem osInfo = Environment.OSVersion;

            osVersionInfo.dwOSVersionInfoSize =
                   Marshal.SizeOf(typeof(OSVERSIONINFOEX));

            if (!GetVersionEx(ref osVersionInfo))
            {
                return null;
            }
            else
            {
                if (osInfo.Version.Major == 5)
                {
                    if (osVersionInfo.wProductType == VER_NT_WORKSTATION)
                    {
                        if (osInfo.Version.Minor == 0)
                        {
                            return WINDOWS_OS.Windows2000;
                        }
                        else
                        {
                            return WINDOWS_OS.WindowsXP;
                        }
                    }
                    else if (osVersionInfo.wProductType == VER_NT_SERVER)
                    {
                        if (osInfo.Version.Minor == 0)
                        {
                            return WINDOWS_OS.Windows2000Server;
                        }
                        else if (osInfo.Version.Minor == 2)
                        {
                            return WINDOWS_OS.WindowsServer2003;
                        }
                    }
                }
                else if (osInfo.Version.Major == 6)
                {
                    if (osVersionInfo.wProductType == VER_NT_WORKSTATION)
                    {
                        if (osInfo.Version.Minor == 0)
                        {
                            return WINDOWS_OS.WindowsVista;
                        }
                        else if (osInfo.Version.Minor == 1)
                        {
                            return WINDOWS_OS.Windows7;
                        }
                    }
                    else if (osVersionInfo.wProductType == VER_NT_SERVER)
                    {
                        if (osInfo.Version.Minor == 0)
                        {
                            return WINDOWS_OS.WindowsServer2008;
                        }
                        else if (osInfo.Version.Minor == 1)
                        {
                            return WINDOWS_OS.WindowsServer2008R2;
                        }
                    }
                }
            }

            return null;
        }
    }
}
