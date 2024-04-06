using CloudVeil.Core.Extensions;
using CloudVeil.Core.Windows.Util.Update;
using CloudVeil.IPC;
using CloudVeil.IPC.Messages;
using Filter.Platform.Common;
using Filter.Platform.Common.Extensions;
using Filter.Platform.Common.IPC.Messages;
using Filter.Platform.Common.Types;
using Filter.Platform.Common.Util;
using FilterProvider.Common.Configuration;
using FilterProvider.Common.Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace FilterProvider.Common.Util
{
    public class UpdateSystem
    {
        public UpdateSystem(IPolicyConfiguration configuration, IPCServer server, string platformId)
        {
            platformPaths = PlatformTypes.New<IPathProvider>();
            systemServices = PlatformTypes.New<ISystemServices>();

            ipcServer = server;
            policyConfiguration = configuration;

            this.platformId = platformId;

            var bitVersionUri = string.Empty;
            if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                bitVersionUri = "/update/cv4w-arm64/update.xml";
            }
            else if(RuntimeInformation.ProcessArchitecture == Architecture.X86)
            {
                bitVersionUri = "/update/cv4w-x86/update.xml";
            }
            else
            {
                bitVersionUri = "/update/cv4w-x64/update.xml";
            }

            var appUpdateInfoUrl = string.Format("{0}{1}?acid={2}&v={3}&os={4}",
                WebServiceUtil.Default.ServiceProviderApiPath,
                bitVersionUri,
                HttpUtility.UrlEncode(FingerprintService.Default.Value),
                Assembly.GetExecutingAssembly().GetName().Version.ToString(),
                WebServiceUtil.GetOsVersion());

            updater = new AppcastUpdater(new Uri(appUpdateInfoUrl));

            logger = LoggerUtil.GetAppWideLogger();
        }

        private string platformId;

        private NLog.Logger logger = null;

        private IPCServer ipcServer = null;

        private IPolicyConfiguration policyConfiguration = null;

        private AppcastUpdater updater = null;

        private ReaderWriterLockSlim appcastUpdaterLock = new ReaderWriterLockSlim();

        private ApplicationUpdate lastFetchedUpdate = null;

        private IPathProvider platformPaths = null;

        private ISystemServices systemServices = null;

        /// <summary>
        /// Initializes the update environment. DOES NOT start CloudVeilUpdater.
        /// </summary>
        private void initializeUpdateEnvironment()
        {
            Task.Delay(200).Wait();
             
            logger.Info("Shutting down to update.");

            if (appcastUpdaterLock.IsWriteLockHeld)
            {
                appcastUpdaterLock.ExitWriteLock();
            }

            if (lastFetchedUpdate.IsRestartRequired)
            {
                string restartFlagPath = Path.Combine(platformPaths.ApplicationDataFolder, "restart.flag");
                using (StreamWriter writer = File.CreateText(restartFlagPath))
                {
                    writer.Write("# This file left intentionally blank (tee-hee)\n");
                }
            }

            // Save auth token when shutting down for update.
            string appDataPath = platformPaths.ApplicationDataFolder;

            try
            {
                if (StringExtensions.Valid(WebServiceUtil.Default.AuthToken))
                {
                    string authTokenPath = Path.Combine(appDataPath, "authtoken.data");

                    using (StreamWriter writer = File.CreateText(authTokenPath))
                    {
                        writer.Write(WebServiceUtil.Default.AuthToken);
                    }
                }

                if (StringExtensions.Valid(WebServiceUtil.Default.UserEmail))
                {
                    string emailPath = Path.Combine(appDataPath, "email.data");

                    using (StreamWriter writer = File.CreateText(emailPath))
                    {
                        writer.Write(WebServiceUtil.Default.UserEmail);
                    }
                }
            }
            catch (Exception e)
            {
                logger.Warn("Could not save authtoken or email before update.");
                LoggerUtil.RecursivelyLogException(logger, e);
            }
        }

        public bool OnRequestUpdate(IpcMessage msg)
        {
            msg.SendReply<ApplicationUpdate>(ipcServer, IpcCall.Update, lastFetchedUpdate);
            return true;
        }

        public bool OnUpdateDialogResult(IpcMessage<UpdateDialogResult> msg)
        {
            try
            {
                appcastUpdaterLock.EnterWriteLock();

                if (msg.Data == UpdateDialogResult.RemindLater)
                {
                    AppSettings.Default.RemindLater = DateTime.Now.AddDays(1);
                    AppSettings.Default.Save();
                }

                if (msg.Data != UpdateDialogResult.UpdateNow)
                {
                    return true;
                }

                if (lastFetchedUpdate != null)
                {
                    ipcServer.Send<object>(IpcCall.InstallerDownloadStarted, null);

                    lastFetchedUpdate.DownloadUpdate((sender, e) =>
                    {
                        ipcServer.Send<int>(IpcCall.InstallerDownloadProgress, e.ProgressPercentage);
                    }).ContinueWith((task) =>
                    {
                        bool updateDownloadResult = task.Result;

                        ipcServer.Send<bool>(IpcCall.InstallerDownloadFinished, updateDownloadResult);

                        if(!updateDownloadResult)
                        {
                            return;
                        }

                        lastFetchedUpdate.BeginInstallUpdate();

                        initializeUpdateEnvironment();
                        Environment.Exit((int)ExitCodes.ShutdownForUpdate);
                    });
                }
            }
            catch (Exception e)
            {
                LoggerUtil.RecursivelyLogException(logger, e);
            }
            finally
            {
                if (appcastUpdaterLock.IsWriteLockHeld)
                {
                    appcastUpdaterLock.ExitWriteLock();
                }
            }

            return true;
        }

        /// <summary>
        /// Checks for application updates, and notifies the GUI of the result.
        /// </summary>
        /// <param name="isCheckButton"></param>
        /// <param name="update"></param>
        /// <returns></returns>
        public UpdateCheckResult ProbeMasterForApplicationUpdates(bool isCheckButton)
        {
            bool hadError = false;
            UpdateCheckResult result = UpdateCheckResult.CheckFailed;

            try
            {
                appcastUpdaterLock.EnterWriteLock();

                if (policyConfiguration.Configuration != null)
                {
                    var config = policyConfiguration.Configuration;
                    lastFetchedUpdate = updater.GetLatestUpdate(config != null ? config.UpdateChannel : string.Empty).Result;
                }
                else
                {
                    logger.Info("No configuration downloaded yet. Skipping application update checks.");
                }

                result = UpdateCheckResult.UpToDate;

                if (lastFetchedUpdate?.IsNewerThan(lastFetchedUpdate?.CurrentVersion) == true)
                {
                    logger.Info("Found update. Asking clients to accept update.");

                    if (!isCheckButton && AppSettings.Default.CanUpdate())
                    {
                        systemServices.EnsureGuiRunning(true);
                        Task.Delay(500).Wait();

                        ipcServer.Send<ApplicationUpdate>(IpcCall.Update, lastFetchedUpdate);
                        ipcServer.Send<UpdateCheckInfo>(IpcCall.CheckForUpdates, new UpdateCheckInfo(AppSettings.Default.LastUpdateCheck, result));
                    }

                    result = UpdateCheckResult.UpdateAvailable;
                }
                else if (lastFetchedUpdate != null)
                {
                    result = UpdateCheckResult.UpToDate;

                    // We send an update check response here for only !isCheckButton because the other case returns a reply directly to its request message.
                    if (!isCheckButton)
                    {
                        ipcServer.Send<UpdateCheckInfo>(IpcCall.CheckForUpdates, new UpdateCheckInfo(AppSettings.Default.LastUpdateCheck, result));
                    }
                }
            }
            catch (Exception e)
            {
                LoggerUtil.RecursivelyLogException(logger, e);
                hadError = true;
            }
            finally
            {
                try
                {
                    switch (result)
                    {
                        case UpdateCheckResult.UpdateAvailable:
                        case UpdateCheckResult.UpToDate:
                            AppSettings.Default.LastUpdateCheck = DateTime.Now;
                            break;
                    }

                    AppSettings.Default.UpdateCheckResult = result;
                    AppSettings.Default.Save();
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "AppSettings update threw.");
                }

                appcastUpdaterLock.ExitWriteLock();
            }

            if (!hadError)
            {
                // Notify all clients that we just successfully made contact with the server.
                // We don't set the status here, because we'd have to store it and set it
                // back, so we just directly issue this msg.
                ipcServer.NotifyStatus(FilterStatus.Synchronized);
            }

            return result;
        }
    }
}
