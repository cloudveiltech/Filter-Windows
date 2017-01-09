using GalaSoft.MvvmLight.CommandWpf;
using System;
using System.Diagnostics;
using System.Windows;
using Te.Citadel.UI.Models;
using Te.Citadel.UI.Views;
using Te.Citadel.Util;

namespace Te.Citadel.UI.ViewModels
{
    public class ProviderConditionsViewModel : BaseCitadelViewModel
    {
        /// <summary>
        /// Flag so we only ever go after the remote terms once.
        /// </summary>
        private volatile bool m_gotTerms = false;

        /// <summary>
        /// The model.
        /// </summary>
        private ProviderConditionsModel m_model = new ProviderConditionsModel();

        /// <summary>
        /// Private data member for the public AcceptCommand property.
        /// </summary>
        private RelayCommand m_acceptCommand;

        /// <summary>
        /// Private data member for the public DeclineCommand property.
        /// </summary>
        private RelayCommand m_declineCommand;

        /// <summary>
        /// Command to issue when the user accepts the terms and conditions.
        /// </summary>
        public RelayCommand AcceptCommand
        {
            get
            {
                if(m_acceptCommand == null)
                {
                    m_acceptCommand = new RelayCommand((Action)(() =>
                    {
                        AuthenticatedUserModel.Instance.HasAcceptedTerms = true;
                        RequestViewChangeCommand.Execute(typeof(DashboardView));
                    }));
                }

                return m_acceptCommand;
            }
        }

        /// <summary>
        /// Command to issue when the user declines the terms and conditions.
        /// </summary>
        public RelayCommand DeclineCommand
        {
            get
            {
                if(m_declineCommand == null)
                {
                    m_declineCommand = new RelayCommand((Action)(() =>
                    {
                        Debug.WriteLine("Decline");
                        // Destroy existing/saved user data and redirect back to login.
                        AuthenticatedUserModel.Destroy();
                        RequestViewChangeCommand.Execute(typeof(LoginView));
                    }));
                }

                return m_declineCommand;
            }
        }

        /// <summary>
        /// Binding path for the application terms.
        /// </summary>
        public string Terms
        {
            get
            {
                if(!m_gotTerms)
                {
                    GetTermsAsync();
                }

                return m_model.Terms;
            }
        }

        public ProviderConditionsViewModel()
        {
        }

        /// <summary>
        /// Gets the terms in an asynchronous fashion and calls the property changed trigger on
        /// success.
        /// </summary>
        private async void GetTermsAsync()
        {
            // Seriously, this will just keep asking over and over and over again thanks to WPF
            // probing this property if this fails. So, don't log errors here.
            var terms = await WebServiceUtil.RequestResource("/capi/getterms.php", true);

            if(terms != null && terms.Length > 0)
            {
                m_gotTerms = true;
                m_model.Terms = System.Text.Encoding.UTF8.GetString(terms);

                await Application.Current.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Normal,
                (Action)delegate ()
                {
                    RaisePropertyChanged(nameof(Terms));
                }
                );
            }
            else
            {
                m_model.Terms = string.Empty;
            }
        }
    }
}