/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using Filter.Platform.Common.Util;
using Citadel.IPC;
using Citadel.IPC.Messages;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using CloudVeilGUI.Common;

namespace CloudVeilGUI.IPCHandlers
{
    public class StateChangedCallback : CallbackBase
    {
        private NLog.Logger logger;

        private Timer synchronizingTimer;
        private object synchronizingTimerLockObj = new object();

        public StateChangedCallback(CommonAppServices app) : base(app)
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
                        app.GuiServices.ShowMainScreen();
                    }
                    break;

                case FilterStatus.Running:
                    {
                        app.GuiServices.ShowMainScreen();
                    }
                    break;

                case FilterStatus.Synchronizing:
                    {
                        app.GuiServices.ShowWaitingScreen();

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
                        app.GuiServices.ShowMainScreen();
                    }
                    break;
            }
        }
    }
}
