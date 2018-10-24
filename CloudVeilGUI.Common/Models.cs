using Citadel.IPC;
using Citadel.IPC.Messages;
using CloudVeilGUI.Common.Platform.Gui;
using CloudVeilGUI.IPCHandlers;
using CloudVeilGUI.Models;
using CloudVeilGUI.Platform.Common;
using CloudVeilGUI.ViewModels;
using Filter.Platform.Common;
using Filter.Platform.Common.Client;
using Filter.Platform.Common.Util;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CloudVeilGUI.Common
{
    public class CommonAppServices
    {
        static CommonAppServices()
        {
            s_default = new CommonAppServices();
        }

        static CommonAppServices s_default;

        public static CommonAppServices Default
        {
            get => s_default;
        }

        private IGUIChecks guiChecks;

        public IGuiServices GuiServices { get; private set; }

        public IPCClient IpcClient { get; private set; }

        public ITrayIconController TrayIconController { get; private set; }

        public ModelManager ModelManager { get => ModelManager.Default; }

        public bool RunGuiOnly { get; set; } = false;

        public void Init()
        {
            ModelManager.Default.Register(new MenuViewModel());
            ModelManager.Default.Register(new BlockedPagesModel());

            GuiServices = PlatformTypes.New<IGuiServices>();
        }

        private void RunGuiChecks()
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

        public void OnStart()
        {
            if(RunGuiOnly)
            {
                return;
            }

            RunGuiChecks();

            IpcClient = IPCClient.InitDefault();

            var filterStarter = PlatformTypes.New<IFilterStarter>();
            filterStarter.StartFilter();

            IpcClient.AuthenticationResultReceived = new AuthenticationResultReceivedCallback(this).Callback;
            IpcClient.StateChanged = new StateChangedCallback(this).Callback;

            IpcClient.BlockActionReceived = (args) =>
            {
                var blockedPagesModel = ModelManager.Default.GetModel<BlockedPagesModel>();
                blockedPagesModel?.BlockedPages?.Add(new BlockedPageEntry(args.Category, args.Resource.ToString()));
            };

            IpcClient.ClientToClientCommandReceived = (args) =>
           {
               switch (args.Command)
               {
                   case ClientToClientCommand.ShowYourself:
                       {
                           GuiServices.BringAppToFront();
                       }
                       break;
               }
           };

            var relaxedPolicyHandlers = new RelaxedPolicyHandlers(this);

            IpcClient.RelaxedPolicyExpired = relaxedPolicyHandlers.RelaxedPolicyExpired;
            IpcClient.RelaxedPolicyInfoReceived = relaxedPolicyHandlers.RelaxedPolicyInfoReceived;

            IpcClient.DeactivationResultReceived = new DeactivationResultCallback(this).Callback;

            TrayIconController = PlatformTypes.New<ITrayIconController>();

            var trayIconMenu = new List<StatusIconMenuItem>();

            trayIconMenu.Add(new StatusIconMenuItem("Open", TrayIcon_Open));
            trayIconMenu.Add(StatusIconMenuItem.Separator);
            trayIconMenu.Add(new StatusIconMenuItem("Settings", TrayIcon_OpenSettings));
            trayIconMenu.Add(new StatusIconMenuItem("Use Relaxed Policy", TrayIcon_UseRelaxedPolicy));

            TrayIconController.InitializeIcon(trayIconMenu);
        }

        private void TrayIcon_UseRelaxedPolicy(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private void TrayIcon_OpenSettings(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private void TrayIcon_Open(object sender, EventArgs e)
        {
            GuiServices.BringAppToFront();
        }
    }
}
