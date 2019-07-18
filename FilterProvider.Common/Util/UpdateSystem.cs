using Citadel.Core.Extensions;
using Citadel.Core.Windows.Util.Update;
using Citadel.IPC;
using Citadel.IPC.Messages;
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FilterProvider.Common.Util
{
    public class UpdateSystem
    {
        public UpdateSystem(IPolicyConfiguration configuration, IPCServer server, string platformId)
        {
            m_platformPaths = PlatformTypes.New<IPathProvider>();
            m_systemServices = PlatformTypes.New<ISystemServices>();

            m_ipcServer = server;
            m_policyConfiguration = configuration;

            m_platformId = platformId;

            var bitVersionUri = string.Empty;
            if (Environment.Is64BitProcess)
            {
                bitVersionUri = "/update/cv2-win-x64/update.xml";
            }
            else
            {
                bitVersionUri = "/update/cv2-win-x86/update.xml";
            }

            var appUpdateInfoUrl = string.Format("{0}{1}", WebServiceUtil.Default.ServiceProviderApiPath, bitVersionUri);

            m_updater = new AppcastUpdater(new Uri(appUpdateInfoUrl));

            m_logger = LoggerUtil.GetAppWideLogger();
        }

        private string m_platformId;

        private NLog.Logger m_logger = null;

        private IPCServer m_ipcServer = null;

        private IPolicyConfiguration m_policyConfiguration = null;

        private AppcastUpdater m_updater = null;

        private ReaderWriterLockSlim m_appcastUpdaterLock = new ReaderWriterLockSlim();

        private ApplicationUpdate m_lastFetchedUpdate = null;

        private IPathProvider m_platformPaths = null;

        private ISystemServices m_systemServices = null;

        /// <summary>
        /// Initializes the update environment. DOES NOT start CloudVeilUpdater.
        /// </summary>
        private void initializeUpdateEnvironment()
        {
            Task.Delay(200).Wait();

            m_logger.Info("Shutting down to update.");

            if (m_appcastUpdaterLock.IsWriteLockHeld)
            {
                m_appcastUpdaterLock.ExitWriteLock();
            }

            if (m_lastFetchedUpdate.IsRestartRequired)
            {
                string restartFlagPath = Path.Combine(m_platformPaths.ApplicationDataFolder, "restart.flag");
                using (StreamWriter writer = File.CreateText(restartFlagPath))
                {
                    writer.Write("# This file left intentionally blank (tee-hee)\n");
                }
            }

            // Save auth token when shutting down for update.
            string appDataPath = m_platformPaths.ApplicationDataFolder;

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
                m_logger.Warn("Could not save authtoken or email before update.");
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
        }

        public bool OnRequestUpdate(IpcMessage msg)
        {
            msg.SendReply<ApplicationUpdate>(m_ipcServer, IpcCall.Update, m_lastFetchedUpdate);
            return true;
        }

        public bool OnUpdateDialogResult(IpcMessage<UpdateDialogResult> msg)
        {
            try
            {
                m_appcastUpdaterLock.EnterWriteLock();

                if (msg.Data == UpdateDialogResult.RemindLater)
                {
                    AppSettings.Default.RemindLater = DateTime.Now.AddDays(1);
                    AppSettings.Default.Save();
                }

                if (msg.Data != UpdateDialogResult.UpdateNow)
                {
                    return true;
                }

                if (m_lastFetchedUpdate != null)
                {
                    m_ipcServer.Send<object>(IpcCall.InstallerDownloadStarted, null);

                    m_lastFetchedUpdate.DownloadUpdate((sender, e) =>
                    {
                        m_ipcServer.Send<int>(IpcCall.InstallerDownloadProgress, e.ProgressPercentage);
                    }).ContinueWith((task) =>
                    {
                        bool updateDownloadResult = task.Result;

                        m_ipcServer.Send<bool>(IpcCall.InstallerDownloadFinished, updateDownloadResult);

                        if(!updateDownloadResult)
                        {
                            return;
                        }

                        m_lastFetchedUpdate.BeginInstallUpdate();

                        initializeUpdateEnvironment();
                        Environment.Exit((int)ExitCodes.ShutdownForUpdate);
                    });
                }
            }
            catch (Exception e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
            finally
            {
                if (m_appcastUpdaterLock.IsWriteLockHeld)
                {
                    m_appcastUpdaterLock.ExitWriteLock();
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
                m_appcastUpdaterLock.EnterWriteLock();

                if (m_policyConfiguration.Configuration != null)
                {
                    var config = m_policyConfiguration.Configuration;
                    m_lastFetchedUpdate = m_updater.GetLatestUpdate(config != null ? config.UpdateChannel : string.Empty).Result;
                }
                else
                {
                    m_logger.Info("No configuration downloaded yet. Skipping application update checks.");
                }

                result = UpdateCheckResult.UpToDate;

                if (m_lastFetchedUpdate?.IsNewerThan(m_lastFetchedUpdate?.CurrentVersion) == true)
                {
                    m_logger.Info("Found update. Asking clients to accept update.");

                    if (!isCheckButton && AppSettings.Default.CanUpdate())
                    {
                        m_systemServices.EnsureGuiRunning();
                        Task.Delay(500).Wait();

                        m_ipcServer.Send<ApplicationUpdate>(IpcCall.Update, m_lastFetchedUpdate);
                        m_ipcServer.Send<UpdateCheckInfo>(IpcCall.CheckForUpdates, new UpdateCheckInfo(AppSettings.Default.LastUpdateCheck, result));
                    }

                    result = UpdateCheckResult.UpdateAvailable;
                }
                else if (m_lastFetchedUpdate != null)
                {
                    result = UpdateCheckResult.UpToDate;

                    // We send an update check response here for only !isCheckButton because the other case returns a reply directly to its request message.
                    if (!isCheckButton)
                    {
                        m_ipcServer.Send<UpdateCheckInfo>(IpcCall.CheckForUpdates, new UpdateCheckInfo(AppSettings.Default.LastUpdateCheck, result));
                    }
                }
            }
            catch (Exception e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
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
                    m_logger.Error(ex, "AppSettings update threw.");
                }

                m_appcastUpdaterLock.ExitWriteLock();
            }

            if (!hadError)
            {
                // Notify all clients that we just successfully made contact with the server.
                // We don't set the status here, because we'd have to store it and set it
                // back, so we just directly issue this msg.
                m_ipcServer.NotifyStatus(FilterStatus.Synchronized);
            }

            return result;
        }
    }
}
