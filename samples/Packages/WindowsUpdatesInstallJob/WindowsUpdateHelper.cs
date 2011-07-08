using System;
using System.Collections.Generic;
using System.Text;
using JobManagerInterfaces;
using WUApiLib;

namespace WindowsUpdatesInstallJob
{
    public static class WindowsUpdateHelper
    {
        public struct WindowsUpdateResult
        {
            public bool UpdatesApplied;
            public bool RestartRequired;

            public WindowsUpdateResult(bool updatesApplied, bool restartRequired)
            {
                this.UpdatesApplied = updatesApplied;
                this.RestartRequired = restartRequired;
            }
        }
        public static WindowsUpdateResult InstallUpdates(JobClientInterface jci)
        {
            UpdateSession updateSession = new UpdateSession();
            IUpdateSearcher updateSearcher = updateSession.CreateUpdateSearcher();

            UpdateCollection updatesToDownload = new UpdateCollection();

            jci.LogString("Searching For Updates...");
            ISearchResult sr = updateSearcher.Search("IsInstalled=0 and Type='Software' and AutoSelectOnWebSites=1");

            if (sr.Updates.Count == 0)
            {
                jci.LogString("No New Updates Found");
                return new WindowsUpdateResult(false, false);
            }

            for (int i = 0; i < sr.Updates.Count; i++)
            {
                IUpdate update = sr.Updates[i];
                if (!update.IsDownloaded)
                {
                    if (update.InstallationBehavior.CanRequestUserInput)
                    {
                        jci.LogString("Ignoring update \"" + update.Title + "\" because it could require user input.");
                        continue;
                    }
                    jci.LogString("Queuing Download of :" + update.Title);
                    updatesToDownload.Add(update);
                }
            }

            if (updatesToDownload.Count > 0)
            {
                jci.LogString("Downloading Updates...");
                UpdateDownloader downloader = updateSession.CreateUpdateDownloader();
                downloader.Updates = updatesToDownload;
                downloader.Download();
            }

            UpdateCollection updatesToInstall = new UpdateCollection();

            for (int i = 0; i < sr.Updates.Count; i++)
            {
                IUpdate update = sr.Updates[i];
                if (update.IsDownloaded)
                {
                    if (!update.EulaAccepted)
                    {
                        update.AcceptEula();
                    }
                    if (update.InstallationBehavior.CanRequestUserInput)
                    {
                        jci.LogString("Ignoring update \"" + update.Title + "\" because it could require user input.");
                        continue;
                    }
                    jci.LogString("Queuing Install of :" + update.Title);
                    updatesToInstall.Add(update);
                }
            }

            if (updatesToInstall.Count > 0)
            {
                jci.LogString("Installing Updates...");
                IUpdateInstaller installer = updateSession.CreateUpdateInstaller();
                installer.Updates = updatesToInstall;
                IInstallationResult installationResult = installer.Install();

                jci.LogString("Installation Finished");
                jci.LogString("Update Result Code: " + installationResult.ResultCode.ToString());

                bool rebootRequired = installationResult.RebootRequired;
                if (rebootRequired)
                {
                    jci.LogString("Reboot Required");
                }
                return new WindowsUpdateResult(true, rebootRequired);
            }
            else
            {
                jci.LogString("No New Updates Found");
                return new WindowsUpdateResult(false, false);
            }
        }
    }
}
