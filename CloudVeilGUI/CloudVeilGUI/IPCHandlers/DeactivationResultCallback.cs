using Filter.Platform.Common.Util;
using Citadel.IPC.Messages;
using Filter.Platform.Common;
using System;
using System.Collections.Generic;
using System.Text;
using Xamarin.Forms;
using CloudVeilGUI.Views;
using CloudVeilGUI.CustomFormElements;

namespace CloudVeilGUI.IPCHandlers
{
    public class DeactivationResultCallback : CallbackBase
    {
        NLog.Logger logger;

        public DeactivationResultCallback(App app) : base(app)
        {
            logger = LoggerUtil.GetAppWideLogger();
        }

        public void Callback(DeactivationCommand deactivationCmd)
        {
            logger.Info("Deactivation command is: {0}", deactivationCmd.ToString());

            if (deactivationCmd == DeactivationCommand.Granted)
            {
                IAntitampering antitampering = PlatformTypes.New<IAntitampering>();

                if(antitampering.IsProcessProtected)
                {
                    antitampering.DisableProcessProtection();
                }

                logger.Info("Deactivation request granted on client.");

                // Init the shutdown of this application.
                Environment.Exit((int)ExitCodes.ShutdownWithoutSafeguards);
                
                // TODO: Implement IPlatformApplicationServices if the above turns out to be a BUG.
                //Application.Current.Shutdown(ExitCodes.ShutdownWithoutSafeguards);
            }
            else
            {
                logger.Info("Deactivation request denied on client.");

                Device.BeginInvokeOnMainThread(async () =>
                {
                    string message = null;
                    string title = null;

                    switch (deactivationCmd)
                    {
                        case DeactivationCommand.Requested:
                            message = "Your deactivation request has been received, but approval is still pending.";
                            title = "Request Received";
                            break;

                        case DeactivationCommand.Denied:
                            // A little bit of tact will keep the mob and their pitchforks
                            // from slaughtering us.
                            message = "Your deactivation request has been received, but approval is still pending.";
                            title = "Request Received";
                            //message = "Your deactivation request has been denied.";
                            //title = "Request Denied";
                            break;

                        case DeactivationCommand.Granted:
                            message = "Your request was granted.";
                            title = "Request Granted";
                            break;

                        case DeactivationCommand.NoResponse:
                            message = "Your deactivation request did not reach the server. Check your internet connection and try again.";
                            title = "No Response Received";
                            break;
                    }

                    if(Application.Current.MainPage is MainPage)
                    {
                        await (Application.Current.MainPage as MainPage).PushModal(new PopupPage());
                    }
                });
            }
        }
    }
}
