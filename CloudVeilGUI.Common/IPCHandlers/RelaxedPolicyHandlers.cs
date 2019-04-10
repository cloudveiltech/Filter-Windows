/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using Citadel.IPC;
using Citadel.IPC.Messages;
using CloudVeilGUI.Common;
using CloudVeilGUI.ViewModels;
using Filter.Platform.Common;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CloudVeilGUI.IPCHandlers
{
    public class RelaxedPolicyHandlers : CallbackBase
    {
        public RelaxedPolicyHandlers(CommonAppServices app) : base(app)
        {

        }

        public void RelaxedPolicyExpired()
        {
            // We don't have to do anything here on our side, but we may want to do something
            // here in the future if we modify how our UI shows relaxed policy timer stuff.
            // Like perhaps changing views etc.
        }

        public void RelaxedPolicyInfoReceived(RelaxedPolicyMessage args)
        {
            var viewModel = app.ModelManager.GetModel<RelaxedPolicyViewModel>();

            if (viewModel != null && args.PolicyInfo != null)
            {
                viewModel.AvailableRelaxedRequests = args.PolicyInfo.NumberAvailableToday;
                viewModel.RelaxedDuration = new DateTime(args.PolicyInfo.RelaxDuration.Ticks).ToString("HH:mm");

                viewModel.RelaxedPolicyRequested -= OnRelaxedPolicyRequested;
                viewModel.RelaxedPolicyRequested += OnRelaxedPolicyRequested;

                viewModel.RelinquishRelaxedPolicyRequested -= OnRelinquishRelaxedPolicyRequested;
                viewModel.RelinquishRelaxedPolicyRequested += OnRelinquishRelaxedPolicyRequested;
            }
        }

        private void OnRelaxedPolicyRequested()
        {
            OnRelaxedPolicyRequested(false);
        }

        public async void OnRelaxedPolicyRequested(bool fromTray)
        {
            using (var ipcClient = new IPCClient())
            {
                ipcClient.ConnectedToServer = () =>
                {
                    ipcClient.RequestRelaxedPolicy("");
                };

                ipcClient.RelaxedPolicyInfoReceived += delegate (RelaxedPolicyMessage msg)
                {
                    ShowRelaxedPolicyMessage(msg, fromTray);
                };

                ipcClient.WaitForConnection();
                await Task.Delay(3000);
            }
        }

        private async void OnRelinquishRelaxedPolicyRequested()
        {
            using (var ipcClient = new IPCClient())
            {
                ipcClient.ConnectedToServer = () =>
                {
                    ipcClient.RelinquishRelaxedPolicy();
                };

                ipcClient.RelaxedPolicyInfoReceived += delegate (RelaxedPolicyMessage msg)
                {
                    ShowRelaxedPolicyMessage(msg, false);
                };

                ipcClient.WaitForConnection();
                await Task.Delay(3000);
            }
        }

        private void ShowRelaxedPolicyMessage(RelaxedPolicyMessage msg, bool fromTray)
        {
            string title = "Relaxed Policy";
            string message = "";

            switch (msg.PolicyInfo.Status)
            {
                case RelaxedPolicyStatus.Activated:
                    message = null;
                    break;

                case RelaxedPolicyStatus.Granted:
                    message = "Relaxed Policy Granted";
                    break;

                case RelaxedPolicyStatus.AllUsed:
                    message = "All of your relaxed policies are used up for today.";
                    break;

                case RelaxedPolicyStatus.AlreadyRelinquished:
                    message = "Relaxed policy not currently active.";
                    break;

                case RelaxedPolicyStatus.Deactivated:
                    message = null;
                    break;

                case RelaxedPolicyStatus.Relinquished:
                    message = null;
                    break;

                default:
                    message = null;
                    break;
            }

            if (fromTray)
            {
                if (message != null)
                {
                    app.TrayIconController.ShowNotification(title, message);
                }
            }
            else
            {
                if(message != null)
                {
                    app.GuiServices.DisplayAlert(title, message, "OK");
                }
            }
        }
    }
}
