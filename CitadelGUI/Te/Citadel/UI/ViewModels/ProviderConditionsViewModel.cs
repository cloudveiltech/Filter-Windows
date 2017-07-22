/*
* Copyright © 2017 Jesse Nicholson  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using GalaSoft.MvvmLight.CommandWpf;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Resources;
using Te.Citadel.UI.Models;
using Te.Citadel.UI.Views;
using Te.Citadel.Util;

namespace Te.Citadel.UI.ViewModels
{
    public class ProviderConditionsViewModel : BaseCitadelViewModel
    {
        private string m_terms;

        private volatile bool m_haveTerms = false;

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
                        WebServiceUtil.Default.HasAcceptedTerms = true;
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
                        WebServiceUtil.Destroy();
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
                return m_terms;
            }
        }

        public bool HaveTerms
        {
            get
            {
                return m_haveTerms;
            }
        }

        public ProviderConditionsViewModel()
        {
            GetTermsAsync();
        }

        /// <summary>
        /// Gets the terms in an asynchronous fashion and calls the property changed trigger on
        /// success.
        /// </summary>
        private async void GetTermsAsync()
        {   
            var userTermsUri = @"pack://application:,,,/Resources/UserLicense.txt";
            byte[] userTermsBytes = null;
            StreamResourceInfo enSentModelInfo = System.Windows.Application.GetResourceStream(new Uri(userTermsUri));

            using(var memoryStream = new MemoryStream())
            {
                enSentModelInfo.Stream.CopyTo(memoryStream);
                userTermsBytes = memoryStream.ToArray();

                m_haveTerms = true;
                m_terms = System.Text.Encoding.UTF8.GetString(userTermsBytes);

                await Application.Current.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Normal,
                    (Action)delegate ()
                    {
                        RaisePropertyChanged(nameof(Terms));
                        RaisePropertyChanged(nameof(HaveTerms));
                    }
                );
            }
        }
    }
}