/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Filter.Platform.Common;
using CloudVeil.IPC;
using CloudVeil.IPC.Messages;
using GalaSoft.MvvmLight;
using System;
using System.Security;
using System.Threading.Tasks;
using System.Windows;
using Gui.CloudVeil.UI.ViewModels;
using Filter.Platform.Common.Extensions;

namespace Gui.CloudVeil.UI.Models
{
    internal class LoginModel : ObservableObject
    {
        private volatile bool currentlyAuthenticating = false;

        private string errorMessage;

        private string userName;

        private LoginViewModel loginViewModel;

        private SecureString userPassword;

        public bool CurrentlyAuthenticating
        {
            get
            {
                return currentlyAuthenticating;
            }
        }

        public string ErrorMessage
        {
            get
            {
                return errorMessage;
            }

            set
            {
                errorMessage = value;
            }
        }

        public string Message { get; set; }

        public string UserName
        {
            get
            {
                return userName;
            }

            set
            {
                userName = value;
            }
        }

        public SecureString UserPassword
        {
            get
            {
                return userPassword;
            }

            set
            {
                userPassword = value;
            }
        }

        /// <summary>
        /// Determines whether or not the current state permits initiating an authentication request. 
        /// </summary>
        /// <returns>
        /// True if the current state is valid for an authentication request, false otherwise. 
        /// </returns>
        public bool CanAttemptAuthenticationWithPassword()
        {
            // Can't auth when we're in the middle of it.
            if(currentlyAuthenticating)
            {
                return false;
            }

            // Ensure some sort of username has been supplied.
            if(!StringExtensions.Valid(UserName))
            {
                return false;
            }

            // Ensure some sort of password has been supplied.
            if(UserPassword == null || UserPassword.Length <= 0)
            {
                return false;
            }

            return true;
        }
        public bool CanAttemptAuthenticationWithEmail()
        {
            // Can't auth when we're in the middle of it.
            if (currentlyAuthenticating)
            {
                return false;
            }

            // Ensure some sort of username has been supplied.
            if (!StringExtensions.Valid(UserName))
            {
                return false;
            }

            try
            {
                var addr = new System.Net.Mail.MailAddress(UserName);
                if(addr.Address != UserName)
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }

            return true;
        }

        public async Task AuthenticateWithPassword()
        {
            ErrorMessage = string.Empty;

            var unencrypedPwordBytes = this.userPassword.SecureStringBytes();

            try
            {
                // Clear error message before running the authentication again. Makes it clearer to the user what's going on.
                loginViewModel.ErrorMessage = "";
                loginViewModel.Message = "";

                await Task.Run(() =>
                {
                    using(var ipcClient = new IPCClient())
                    {
                        ipcClient.ConnectedToServer = () =>
                        {
                            ipcClient.AttemptAuthenticationWithPassword(userName, userPassword);
                        };

                        ipcClient.AuthenticationResultReceived = (msg) =>
                        {
                            if (msg.AuthenticationResult.AuthenticationMessage != null)
                            {
                                loginViewModel.ErrorMessage = msg.AuthenticationResult.AuthenticationMessage;
                            }
                        };

                        ipcClient.WaitForConnection();
                        Task.Delay(3000).Wait();
                    }
                });
            }
            finally
            {
                // Always purge password from memory ASAP.
                if(unencrypedPwordBytes != null && unencrypedPwordBytes.Length > 0)
                {
                    Array.Clear(unencrypedPwordBytes, 0, unencrypedPwordBytes.Length);
                }
            }
        }

        public async Task AuthenticateWithEmail()
        {
            ErrorMessage = string.Empty;

            // Clear error message before running the authentication again. Makes it clearer to the user what's going on.
            loginViewModel.ErrorMessage = "";
            loginViewModel.Message = "";

            await Task.Run(() =>
            {
                using (var ipcClient = new IPCClient())
                {
                    ipcClient.ConnectedToServer = () =>
                    {
                        ipcClient.AttemptAuthenticationWithEmail(userName);
                        loginViewModel.Message = "Request sent. Please check your E-Mail.";
                    };

                    ipcClient.AuthenticationResultReceived = (msg) =>
                    {
                        if (msg.AuthenticationResult.AuthenticationMessage != null)
                        {
                            loginViewModel.ErrorMessage = msg.AuthenticationResult.AuthenticationMessage;
                        }
                        loginViewModel.hideProgessView();
                    };

                    ipcClient.WaitForConnection();
                    Task.Delay(30000).Wait();
                }
            });
        }


        public LoginModel(LoginViewModel viewModel)
        {
            UserName = string.Empty;
            UserPassword = new SecureString();
            loginViewModel = viewModel;
        }
    }
}