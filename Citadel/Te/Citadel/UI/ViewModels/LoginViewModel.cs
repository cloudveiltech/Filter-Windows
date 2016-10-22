using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Te.Citadel.Extensions;
using Te.Citadel.UI.Models;
using Te.Citadel.UI.Views;

namespace Te.Citadel.UI.ViewModels
{
    
    /// <summary>
    /// The LoginViewModel class serves as the ViewModel for the LoginView UserControl.
    /// </summary>
    public class LoginViewModel : BaseCitadelViewModel
    {
        /// <summary>
        /// The model.
        /// </summary>
        private LoginModel m_model = new LoginModel();

        /// <summary>
        /// Private data member for the public AuthenticateCommand property.
        /// </summary>
        private RelayCommand m_authenticateCommand;        

        /// <summary>
        /// Command to run an authentication request for the credentials given in the view.
        /// CanExecute looks to the model to see if the current state permits execution of this
        /// action. The actual command itself is sent to the model.
        /// </summary>
        public RelayCommand AuthenticateCommand
        {
            get
            {
                if(m_authenticateCommand == null)
                {
                    m_authenticateCommand = new RelayCommand((Action)(async ()=>
                    {
                        try
                        {   
                            RequestViewChangeCommand.Execute(typeof(ProgressWait));
                            var authSuccess = await m_model.Authenticate();

                            if(!authSuccess)
                            {
                                RequestViewChangeCommand.Execute(typeof(LoginView));
                            }
                            else
                            {
                                RequestViewChangeCommand.Execute(typeof(ProviderConditionsView));
                            }
                        }
                        catch(Exception e)
                        {
                            Debug.WriteLine(e.Message);
                        }
                        
                    }), m_model.CanAttemptAuthentication);
                }
                
                return m_authenticateCommand;
            }
        }       

        /// <summary>
        /// Binding path for the service provider input field.
        /// </summary>
        public string ServiceProvider
        {
            get
            {
                return m_model.ServiceProvider;
            }

            set
            {
                if (value != null && !value.OIEquals(m_model.ServiceProvider))
                {   
                    m_authenticateCommand.RaiseCanExecuteChanged();

                    m_model.ServiceProvider = value;
                    RaisePropertyChanged(nameof(ServiceProvider));
                }
            }
        }

        /// <summary>
        /// Binding path for user feedback error messages.
        /// </summary>
        public string ErrorMessage
        {
            get
            {
                return m_model.ErrorMessage;
            }

            set
            {
                if(value != null && !value.OIEquals(m_model.ErrorMessage))
                {
                    m_authenticateCommand.RaiseCanExecuteChanged();

                    m_model.ErrorMessage = value;
                    RaisePropertyChanged(nameof(ErrorMessage));
                }
            }
        }

        /// <summary>
        /// Binding path for the username input field.
        /// </summary>
        public string UserName
        {
            get
            {                
                return m_model.UserName;
            }

            set
            {
                if(value != null && !value.OIEquals(m_model.UserName))
                {
                    m_authenticateCommand.RaiseCanExecuteChanged();
                    m_model.UserName = value;
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
                return m_model.UserPassword;
            }

            set
            {
                if (value != null && !value.OEquals(m_model.UserPassword))
                {
                    m_authenticateCommand.RaiseCanExecuteChanged();

                    m_model.UserPassword = value;
                    RaisePropertyChanged(nameof(UserPassword));
                }
            }
        }
    }
}
