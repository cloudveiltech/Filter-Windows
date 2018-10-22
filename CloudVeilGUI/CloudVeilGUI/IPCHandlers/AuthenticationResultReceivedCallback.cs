/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using Citadel.IPC.Messages;
using CloudVeilGUI.Views;
using System;
using System.Collections.Generic;
using System.Text;
using Xamarin.Forms;

namespace CloudVeilGUI.IPCHandlers
{
    public class AuthenticationResultReceivedCallback : CallbackBase
    {

        public AuthenticationResultReceivedCallback(App app) : base(app)
        {

        }

        public void Callback(AuthenticationMessage authenticationFailureResult)
        {
            switch (authenticationFailureResult.Action)
            {
                case AuthenticationAction.Denied:
                case AuthenticationAction.Required:
                case AuthenticationAction.InvalidInput:
                    {
                        // User needs to log in.
                        //BringAppToFocus();

                        Device.BeginInvokeOnMainThread(async () =>
                        {
                            if(!(app.NavPage.CurrentPage is LoginPage))
                            {
                                await app.NavPage.PushAsync(new LoginPage());
                            }
                        });

                        /*m_mainWindow.Dispatcher.InvokeAsync(() =>
                        {
                            ((MainWindowViewModel)m_mainWindow.DataContext).IsUserLoggedIn = false;
                        });*/
                    }
                    break;

                case AuthenticationAction.Authenticated:
                case AuthenticationAction.ErrorNoInternet:
                case AuthenticationAction.ErrorUnknown:
                    {
                        Device.BeginInvokeOnMainThread(async () =>
                        {
                            if (app.NavPage.CurrentPage is LoginPage)
                            {
                                await app.NavPage.PopToRootAsync();
                            }
                        });

                        /*m_logger.Info($"The logged in user is {authenticationFailureResult.Username}");

                        m_mainWindow.Dispatcher.InvokeAsync(() =>
                        {
                            ((MainWindowViewModel)m_mainWindow.DataContext).LoggedInUser = authenticationFailureResult.Username;
                            ((MainWindowViewModel)m_mainWindow.DataContext).IsUserLoggedIn = true;
                        });

                        OnViewChangeRequest(typeof(DashboardView));*/
                    }
                    break;
            }
        }
    }
}
