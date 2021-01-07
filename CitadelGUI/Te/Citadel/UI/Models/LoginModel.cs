/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Filter.Platform.Common;
using Citadel.IPC;
using Citadel.IPC.Messages;
using GalaSoft.MvvmLight;
using System;
using System.Security;
using System.Threading.Tasks;
using System.Windows;
using Te.Citadel.UI.ViewModels;
using Filter.Platform.Common.Extensions;

namespace Te.Citadel.UI.Models
{
    internal class LoginModel : ObservableObject
    {
        private volatile bool m_currentlyAuthenticating = false;

        private string m_errorMessage;

        private string m_userName;

        private LoginViewModel m_loginViewModel;

        private SecureString m_userPassword;

        public bool CurrentlyAuthenticating
        {
            get
            {
                return m_currentlyAuthenticating;
            }
        }

        public string ErrorMessage
        {
            get
            {
                return m_errorMessage;
            }

            set
            {
                m_errorMessage = value;
            }
        }

        public string Message { get; set; }

        public string UserName
        {
            get
            {
                return m_userName;
            }

            set
            {
                m_userName = value;
            }
        }

        public SecureString UserPassword
        {
            get
            {
                return m_userPassword;
            }

            set
            {
                m_userPassword = value;
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
            if(m_currentlyAuthenticating)
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
            if (m_currentlyAuthenticating)
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

            var unencrypedPwordBytes = this.m_userPassword.SecureStringBytes();

            try
            {
                // Clear error message before running the authentication again. Makes it clearer to the user what's going on.
                m_loginViewModel.ErrorMessage = "";
                m_loginViewModel.Message = "";

                await Task.Run(() =>
                {
                    using(var ipcClient = new IPCClient())
                    {
                        ipcClient.ConnectedToServer = () =>
                        {
                            ipcClient.AttemptAuthenticationWithPassword(m_userName, m_userPassword);
                        };

                        ipcClient.AuthenticationResultReceived = (msg) =>
                        {
                            if (msg.AuthenticationResult.AuthenticationMessage != null)
                            {
                                m_loginViewModel.ErrorMessage = msg.AuthenticationResult.AuthenticationMessage;
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
            m_loginViewModel.ErrorMessage = "";
            m_loginViewModel.Message = "";

            await Task.Run(() =>
            {
                using (var ipcClient = new IPCClient())
                {
                    ipcClient.ConnectedToServer = () =>
                    {
                        ipcClient.AttemptAuthenticationWithEmail(m_userName);
                        m_loginViewModel.Message = "Request sent. Please check your E-Mail.";
                    };

                    ipcClient.AuthenticationResultReceived = (msg) =>
                    {
                        if (msg.AuthenticationResult.AuthenticationMessage != null)
                        {
                            m_loginViewModel.ErrorMessage = msg.AuthenticationResult.AuthenticationMessage;
                        }
                        m_loginViewModel.hideProgessView();
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
            m_loginViewModel = viewModel;
        }
    }
}