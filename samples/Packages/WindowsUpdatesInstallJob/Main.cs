using System;
using System.Collections.Generic;
using System.Text;
using JobManagerInterfaces;
using System.IO;

namespace WindowsUpdatesInstallJob
{
    public class Main : JobRunner
    {
        public ExecutionResult Execute(JobClientInterface jci)
        {
            jci.ShutdownOnCompletion = true;

            WindowsUpdateHelper.WindowsUpdateResult updateResult;
            try
            {
                updateResult = WindowsUpdateHelper.InstallUpdates(jci);
            }
            catch (Exception ex)
            {
                jci.ShutdownOnCompletion = false;
                ExecutionResult jr = new ExecutionResult(ex.ToString(), null);
                return jr;
            }

            if (updateResult.RestartRequired)
            {
                jci.SetPropertyValue("updatesApplied", "true");
                jci.StartupOnNextRun();
                System.Diagnostics.Process.Start("shutdown", "-r");
                return null;
            }
            else
            {
                //reset the autostart functunality for jobmanagerclient
                FileInfo regFile = new FileInfo(Path.Combine(jci.WorkingDir.FullName, "..\\..\\run_once_automation.reg"));
                System.Diagnostics.Process.Start("regedit", "/s \"" + regFile.FullName + "\"");

                ExecutionResult jr = new ExecutionResult();
                //if updates were applied, instruct the framework to take a snapshot of the VM after shutdown
                string updatesAppliedProperty = jci.GetPropertyValue("updatesApplied");
                bool updatesAppliedBeforeReboot = updatesAppliedProperty != null && updatesAppliedProperty == "true";
                if (updateResult.UpdatesApplied || updatesAppliedBeforeReboot)
                {
                    jr.SnapshotOnShutdown = true;
                    jr.SnapshotDesc = "WU Applied";
                }
                jr.Success = true;
                return jr;
            }
        }
    }
}
