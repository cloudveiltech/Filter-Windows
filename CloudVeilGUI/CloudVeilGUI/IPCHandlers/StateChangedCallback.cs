/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using Citadel.Core.Windows.Util;
using Citadel.IPC;
using Citadel.IPC.Messages;
using CloudVeilGUI.Views;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Xamarin.Forms;

namespace CloudVeilGUI.IPCHandlers
{
    public class StateChangedCallback : CallbackBase
    {
        private NLog.Logger logger;

        private Timer synchronizingTimer;
        private object synchronizingTimerLockObj = new object();

        public StateChangedCallback(App app) : base(app)
        {
            logger = LoggerUtil.GetAppWideLogger();
        }

        public void Callback(StateChangeEventArgs args)
        {
            logger.Info("Filter status from server is: {0}", args.State.ToString());

            switch (args.State)
            {
                case FilterStatus.CooldownPeriodEnforced:
                    {
                        Device.BeginInvokeOnMainThread(() =>
                        {
                            // TODO: Add disabled internet message on filter.
                        });
                    }
                    break;

                case FilterStatus.Running:
                    {
                        Device.BeginInvokeOnMainThread(() =>
                        {
                            if (!(app.MainPage is MainPage))
                            {
                                app.MainPage = new MainPage();
                            }

                            // TODO: Hide disabled internet message.
                        });
                    }
                    break;

                case FilterStatus.Synchronizing:
                    {
                        Device.BeginInvokeOnMainThread(() =>
                        {
                            if (!(app.MainPage is WaitingPage))
                            {
                                app.MainPage = new WaitingPage();
                            }
                        });

                        lock (synchronizingTimerLockObj)
                        {
                            if (synchronizingTimer != null)
                            {
                                synchronizingTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                                synchronizingTimer.Dispose();
                                synchronizingTimer = null;
                            }

                            synchronizingTimer = new Timer((state) =>
                            {
                                app.IpcClient.RequestStatusRefresh();
                                synchronizingTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                                synchronizingTimer.Dispose();
                                synchronizingTimer = null;
                            });

                            synchronizingTimer.Change(TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);
                        }
                    }
                    break;

                case FilterStatus.Synchronized:
                    {
                        Device.BeginInvokeOnMainThread(() =>
                        {
                            if (!(app.MainPage is MainPage))
                            {
                                app.MainPage = new MainPage();
                            }

                            // TODO: Update dashboard view model with last sync time.
                            // TODO: Add last sync time somewhere.
                        });
                        
                    }
                    break;
            }
        }
    }
}
