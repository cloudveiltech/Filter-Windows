/*
* Copyright © 2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿
using System;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using CloudVeilGUI.Views;
using CloudVeilGUI.Platform.Common;
using Citadel.IPC;
using CloudVeilGUI.Models;
using Citadel.IPC.Messages;
using System.Collections;
using System.Collections.Generic;
using Citadel.Core.Windows.Util;
using Filter.Platform.Common;
using Filter.Platform.Common.Client;
using System.Threading;
using Te.Citadel.Util;
using System.Threading.Tasks;
using CloudVeilGUI.IPCHandlers;

[assembly: XamlCompilation(XamlCompilationOptions.Compile)]
namespace CloudVeilGUI
{
    public partial class App : Application
    {
        private Mutex instanceMutex = null;

        private IPCClient m_ipcClient;
        private NLog.Logger logger;

        public ModelManager ModelManager { get; private set; }

        /// <summary>
        /// This is a stack for preserved pages in case one page needs to override another.
        /// </summary>
        public Stack<Page> PreservedPages { get; set; }

        public IPCClient IpcClient
        {
            get { return m_ipcClient; }
        }

        private bool guiOnly;

        public App(bool guiOnly = false)
        {
            this.guiOnly = guiOnly;

            InitializeComponent();

            PreservedPages = new Stack<Page>();

            ModelManager = new ModelManager();
            ModelManager.Register(new BlockedPagesModel());

            // Code smell: MainPage() makes use of ModelManager, so we need to instantiate ModelManager first.
            MainPage = guiOnly ? (Page)new MainPage() : (Page)new WaitingPage();
        }

        private void RunGuiChecks()
        {
            var guiChecks = PlatformTypes.New<IGUIChecks>();

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
                string appVerStr = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                appVerStr += "." + System.Reflection.AssemblyName.GetAssemblyName(assembly.Location).Version.ToString();

                bool createdNew;
                try
                {
                    instanceMutex = new Mutex(true, $"Local\\{GuidUtility.Create(GuidUtility.DnsNamespace, appVerStr).ToString("B")}", out createdNew);
                }
                catch
                {
                    // We can get access denied if SYSTEM is running this.
                    createdNew = false;
                }

                if (!createdNew)
                {
                    try
                    {
                        guiChecks.DisplayExistingUI();
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
                            ipcClient.RequestPrimaryClientShowUI();

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

        protected override void OnStart()
        {
            if(guiOnly)
            {
                return;
            }

            RunGuiChecks();

            MainPage = new WaitingPage();

            m_ipcClient = IPCClient.InitDefault();

            var filterStarter = PlatformTypes.New<IFilterStarter>();
            filterStarter.StartFilter();

            m_ipcClient.AuthenticationResultReceived = new AuthenticationResultReceivedCallback(this).Callback;
            m_ipcClient.StateChanged = new StateChangedCallback(this).Callback;

            m_ipcClient.BlockActionReceived = (args) =>
            {
                var blockedPagesModel = ModelManager.GetModel<BlockedPagesModel>();
                blockedPagesModel.BlockedPages.Add(new BlockedPageEntry(args.Category, args.Resource.ToString()));
            };

            m_ipcClient.ClientToClientCommandReceived = (args) =>
            {
                switch(args.Command)
                {
                    case ClientToClientCommand.ShowYourself:
                        {
                            var guiServices = PlatformTypes.New<IGuiServices>();
                            guiServices.BringAppToFront();
                        }
                        break;
                }
            };
        }

        /// <summary>
        /// This is a custom callback that should be called by platform specific code when the app is exiting.
        /// </summary>
        public void OnExit()
        {
            try
            {
                instanceMutex.ReleaseMutex();
            }
            catch (Exception e)
            {
                try
                {
                    var logger = LoggerUtil.GetAppWideLogger();
                    LoggerUtil.RecursivelyLogException(logger, e);
                }
                catch (Exception he)
                {
                    // XXX TODO - We can't really log here unless we do a direct-to-file write.
                }
            }
        }

        protected override void OnSleep()
        {
            // Handle when your app sleeps.
        }

        protected override void OnResume()
        {
            // Handle when your app resumes
        }
    }
}
