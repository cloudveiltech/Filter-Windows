/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using Citadel.IPC;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace CloudVeilGUI.ViewModels
{
    public class LoginViewModel : INotifyPropertyChanged
    {
        private bool currentlyAuthenticating;
        public bool CurrentlyAuthenticating
        {
            get
            {
                return currentlyAuthenticating;
            }

            set
            {
                currentlyAuthenticating = value;
                OnPropertyChanged(nameof(CurrentlyAuthenticating));
            }
        }

        private string errorMessage;
        public string ErrorMessage
        {
            get
            {
                return errorMessage;
            }

            set
            {
                errorMessage = value;
                OnPropertyChanged(nameof(ErrorMessage));
            }
        }

        private string username;
        public string Username
        {
            get
            {
                return username;
            }

            set
            {
                username = value;
                OnPropertyChanged(nameof(Username));
            }
        }

        private string password;
        public string Password
        {
            get
            {
                return password;
            }

            set
            {
                password = value;
                OnPropertyChanged(nameof(Password));
            }
        }

        private Command authenticateCommand;
        public Command AuthenticateCommand
        {
            get
            {
                if(authenticateCommand == null)
                {
                    authenticateCommand = new Command(async () =>
                    {
                        await Authenticate();
                    });
                }

                return authenticateCommand;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public async Task Authenticate()
        {
            ErrorMessage = "";

            await Task.Run(() =>
            {
                using (var ipcClient = new IPCClient())
                {
                    ipcClient.ConnectedToServer = () =>
                    {
                        // TODO: Rework either password box or login message to not require unsafe code in order to send authentication.
                        SecureString passwordStr = null;
                        unsafe
                        {
                            fixed (char* pass = password)
                            {
                                passwordStr = new SecureString(pass, password.Length);
                            }
                        }

                        ipcClient.AttemptAuthentication(username, passwordStr);
                    };

                    ipcClient.AuthenticationResultReceived = (msg) =>
                    {
                        if(msg.AuthenticationResult.AuthenticationMessage != null)
                        {
                            ErrorMessage = msg.AuthenticationResult.AuthenticationMessage;
                        }
                    };

                    ipcClient.WaitForConnection();
                    Task.Delay(3000).Wait();
                }
            });
        }
    }
}
