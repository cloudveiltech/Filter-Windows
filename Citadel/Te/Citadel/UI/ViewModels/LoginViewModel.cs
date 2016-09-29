using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Te.Citadel.Extensions;
using Te.Citadel.UI.Models;

namespace Te.Citadel.UI.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private LoginModel m_model = new LoginModel();

        private RelayCommand m_authenticateCommand;

        public RelayCommand AuthenticateCommand
        {
            get
            {
                if(m_authenticateCommand == null)
                {
                    m_authenticateCommand = new RelayCommand(m_model.Authenticate, m_model.CanAttemptAuthentication);
                }

                return m_authenticateCommand;
            }
        }

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
