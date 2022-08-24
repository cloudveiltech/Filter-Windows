/*
* Copyright © 2017 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Filter.Platform.Common.Extensions;
using CloudVeil.Core.Windows.Util;
using Filter.Platform.Common.Util;
using GalaSoft.MvvmLight.CommandWpf;
using System;
using System.Security;
using Gui.CloudVeil.Extensions;
using Gui.CloudVeil.UI.Models;
using Gui.CloudVeil.UI.Views;
using Gui.CloudVeil.Util;

namespace Gui.CloudVeil.UI.ViewModels
{
    /// <summary>
    /// The LoginViewModel class serves as the ViewModel for the LoginView UserControl.
    /// </summary>
    public class LoginViewModel : BaseCloudVeilViewModel
    {
        /// <summary>
        /// The model.
        /// </summary>
        private LoginModel model = null;

        /// <summary>
        /// Private data member for the public AuthenticateCommand property.
        /// </summary>
        private RelayCommand authenticateWithPasswordCommand;

        private RelayCommand authenticateWithEmailCommand;

        public LoginViewModel()
        {
            // We have to pass the LoginViewModel into our LoginModel so that changes to the LoginModel can RaisePropertyChanged() on the view model.
            // TODO It would be cleaner to not have two layers of properties like this. Maybe change LoginModel into more of a "function container"
            // and make it do all variable edits directly to LoginViewModel.
            model = new LoginModel(this);
        }

        /// <summary>
        /// Command to run an authentication request for the credentials given in the view.
        /// CanExecute looks to the model to see if the current state permits execution of this
        /// action. The actual command itself is sent to the model.
        /// </summary>
        public RelayCommand AuthenticateWithPasswordCommand
        {
            get
            {
                if(authenticateWithPasswordCommand == null)
                {
                    authenticateWithPasswordCommand = new RelayCommand((Action)(async () =>
                    {
                        try
                        {
                            ViewManager?.PushView(LoginView.ModalZIndex * 2, typeof(ProgressWait));
                            await model.AuthenticateWithPassword();
                            ViewManager?.PopView(typeof(ProgressWait));
                        }
                        catch(Exception e)
                        {
                            LoggerUtil.RecursivelyLogException(logger, e);
                        }
                    }), model.CanAttemptAuthenticationWithPassword);
                }

                return authenticateWithPasswordCommand;
            }
        }

        public RelayCommand AuthenticateWithEmailCommand
        {
            get 
            {
                if (authenticateWithEmailCommand == null)
                {
                    authenticateWithEmailCommand = new RelayCommand((Action)(async () =>
                    {
                        try
                        {
                            ViewManager?.PushView(LoginView.ModalZIndex * 2, typeof(ProgressWait));
                            await model.AuthenticateWithEmail();
                        }
                        catch (Exception e)
                        {
                            LoggerUtil.RecursivelyLogException(logger, e);
                        }
                    }), model.CanAttemptAuthenticationWithEmail);
                }

                return authenticateWithEmailCommand;
            }
        }

        public void hideProgessView()
        {
            ViewManager?.PopView(typeof(ProgressWait));
        }

        /// <summary>
        /// Binding path for user feedback error messages.
        /// </summary>
        public string ErrorMessage
        {
            get
            {
                return model.ErrorMessage;
            }

            set
            {
                model.ErrorMessage = value;
                RaisePropertyChanged(nameof(ErrorMessage));
            }
        }

        public string Message
        {
            get
            {
                return model.Message;
            }

            set
            {
                model.Message = value;
                RaisePropertyChanged(nameof(Message));
            }
        }


        /// <summary>
        /// Binding path for the username input field.
        /// </summary>
        public string UserName
        {
            get
            {
                return model.UserName;
            }

            set
            {
                if(value != null && !value.OIEquals(model.UserName))
                {
                    authenticateWithPasswordCommand.RaiseCanExecuteChanged();
                   
                    model.UserName = value;
                    RaisePropertyChanged(nameof(UserName));
                }
            }
        }

        /// <summary>
        /// Binding path for the password input field.
        /// </summary>
        public SecureString UserPassword
        {
            get
            {
                return model.UserPassword;
            }

            set
            {
                if(value != null && !value.OEquals(model.UserPassword))
                {
                    authenticateWithPasswordCommand.RaiseCanExecuteChanged();

                    model.UserPassword = value;
                    RaisePropertyChanged(nameof(UserPassword));
                }
            }
        }
    }
}