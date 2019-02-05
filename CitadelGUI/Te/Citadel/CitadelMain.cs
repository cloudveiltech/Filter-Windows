/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Citadel.Core.WinAPI;
using Citadel.Core.Windows.Util;
using Citadel.IPC;
using CloudVeil.Windows;
using CloudVeil.Windows.Platform;
using CloudVeilGUI.Platform.Common;
using CloudVeilGUI.Platform.Windows;
using Filter.Platform.Common;
using Filter.Platform.Common.Client;
using Filter.Platform.Common.Util;
using NLog;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Te.Citadel.Util;

namespace CloudVeil.Windows
{
    public static class CitadelMain
    {
        private static Logger MainLogger;

        private static IGUIChecks guiChecks;

        private static void RunGuiChecks(bool startMinimized)
        {
            guiChecks = PlatformTypes.New<IGUIChecks>();

            // First, lets check to see if the user started the GUI in an isolated session.
            try
            {
                if (guiChecks.IsInIsolatedSession())
                {
                    LoggerUtil.GetAppWideLogger().Error("GUI client start in an isolated session. This should not happen.");
                    Environment.Exit((int)ExitCodes.ShutdownWithoutSafeguards);
                    return;
                }
            }
            catch
            {
                Environment.Exit((int)ExitCodes.ShutdownWithoutSafeguards);
                return;
            }

            try
            {
                bool createdNew = false;
                if (guiChecks.PublishRunningApp())
                {
                    createdNew = true;
                }

                /**/

                if (!createdNew)
                {
                    try
                    {
                        if (!startMinimized)
                        {
                            guiChecks.DisplayExistingUI();
                        }
                    }
                    catch (Exception e)
                    {
                        LoggerUtil.RecursivelyLogException(LoggerUtil.GetAppWideLogger(), e);
                    }

                    // In case we have some out of sync state where the app is running at a higher
                    // privilege level than us, the app won't get our messages. So, let's attempt an
                    // IPC named pipe to deliver the message as well.
                    try
                    {
                        // Something about instantiating an IPCClient here is making it all blow up in my face.
                        using (var ipcClient = IPCClient.InitDefault())
                        {
                            if (!startMinimized)
                            {
                                ipcClient.RequestPrimaryClientShowUI();
                            }

                            // Wait plenty of time before dispose to allow delivery of the message.
                            Task.Delay(500).Wait();
                        }
                    }
                    catch (Exception e)
                    {
                        // The only way we got here is if the server isn't running, in which case we
                        // can do nothing because its beyond our domain.
                        LoggerUtil.RecursivelyLogException(LoggerUtil.GetAppWideLogger(), e);
                    }

                    LoggerUtil.GetAppWideLogger().Info("Shutting down process since one is already open.");

                    // Close this instance.
                    Environment.Exit((int)ExitCodes.ShutdownProcessAlreadyOpen);
                    return;
                }
            }
            catch (Exception e)
            {
                // The only way we got here is if the server isn't running, in which case we can do
                // nothing because its beyond our domain.
                LoggerUtil.RecursivelyLogException(LoggerUtil.GetAppWideLogger(), e);
                return;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="args">
        /// </param>
        [STAThread]
        public static void Main(string[] args)
        {
            bool startMinimized = false;

            foreach (string arg in args)
            {
                if (arg.IndexOf("StartMinimized") != -1)
                {
                    startMinimized = true;
                    break;
                }
            }

            try
            {
                if(Process.GetCurrentProcess().SessionId <= 0)
                {
                    try
                    {
                        LoggerUtil.GetAppWideLogger().Error("GUI client started in session 0 isolation. Exiting. This should not happen.");
                        Environment.Exit((int)ExitCodes.ShutdownWithoutSafeguards);
                        return;
                    }
                    catch(Exception e)
                    {
                        // XXX TODO - We can't really log here unless we do a direct to-file write.
                        Environment.Exit((int)ExitCodes.ShutdownWithoutSafeguards);
                        return;
                    }
                }

                Citadel.Core.Windows.Platform.Init();

                PlatformTypes.Register<IFilterStarter>((arr) => new WindowsFilterStarter());
                PlatformTypes.Register<IGuiServices>((arr) => new WindowsGuiServices());
                PlatformTypes.Register<ITrayIconController>((arr) => new WindowsTrayIconController());
            }
            catch
            {
                // Lets assume that if we can't even read our session ID, that we're in session 0.
                Environment.Exit((int)ExitCodes.ShutdownWithoutSafeguards);
                return;
            }

            var guiChecks = PlatformTypes.New<IGUIChecks>();

            try
            {
                RunGuiChecks(startMinimized);
            }
            catch(Exception e)
            {
                // The only way we got here is if the server isn't running, in which case we can do
                // nothing because its beyond our domain.
                LoggerUtil.RecursivelyLogException(LoggerUtil.GetAppWideLogger(), e);
                return;
            }

            try
            {
                MainLogger = LoggerUtil.GetAppWideLogger();
            }
            catch { }

            try
            {
                var app = new CitadelApp();
                app.InitializeComponent();
                app.Run();
                
                // Always release mutex.
                guiChecks.UnpublishRunningApp();
            }
            catch(Exception e)
            {
                try
                {
                    MainLogger = LoggerUtil.GetAppWideLogger();
                    LoggerUtil.RecursivelyLogException(MainLogger, e);
                }
                catch(Exception be)
                {
                    // XXX TODO - We can't really log here unless we do a direct to-file write.
                }
            }

            // No matter what, always ensure that critical flags are removed from our process before exiting.
            CriticalKernelProcessUtility.SetMyProcessAsNonKernelCritical();
        }
    }
}