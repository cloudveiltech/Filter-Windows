/*
* Copyright © 2017 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Citadel.Core.Windows.Types;
using Citadel.Core.Windows.Util;
using Citadel.IPC;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Te.Citadel.Extensions;
using Te.Citadel.UI.Models;
using Te.Citadel.UI.Views;
using Te.Citadel.Util;

namespace Te.Citadel.UI.ViewModels
{
    /// <summary>
    /// Class for displaying block event information in a DataGrid.
    /// </summary>
    public class ViewableBlockedRequests : ObservableObject
    {
        public string CategoryName
        {
            get;
            private set;
        }

        public string FullRequest
        {
            get;
            private set;
        }

        public ViewableBlockedRequests(string category, string fullRequest)
        {
            this.CategoryName = category;
            this.FullRequest = fullRequest;
        }
    }

    public class HistoryViewModel : BaseCitadelViewModel
    {


        /// <summary>
        /// The model.
        /// </summary>
        private DashboardModel m_model = new DashboardModel();

        /// <summary>
        /// List of observable block actions that the user can view.
        /// </summary>
        public ObservableCollection<ViewableBlockedRequests> BlockEvents
        {
            get;
            set;
        }

        public HistoryViewModel()
        {
            BlockEvents = new ObservableCollection<ViewableBlockedRequests>();
        }

        /// <summary>
        /// Private data member for the public DeactivateCommand property.
        /// </summary>
        private RelayCommand m_deactivationCommand;

        /// <summary>
        /// Private data member for the public RequestBlockActionReviewCommand property.
        /// </summary>
        private RelayCommand<ViewableBlockedRequests> m_requestBlockActionReviewCommand;

        /// <summary>
        /// Private data member for the public ViewLogsCommand property.
        /// </summary>
        private RelayCommand m_viewLogsCommand;

        /// <summary>
        /// Private data member for the public UseRelaxedPolicyCommand property.
        /// </summary>
        private RelayCommand m_useRelaxedPolicyCommand;

        /// <summary>
        /// Private data member for the public RelinquishRelaxedPolicyCommand property.
        /// </summary>
        private RelayCommand m_relinquishRelaxedPolicyCommand;

        internal DashboardModel Model
        {
            get
            {
                return m_model;
            }
        }

        private RelayCommand m_sidebarButtonCommand;

        /// <summary>
        /// Generic handler for all of the sidebar buttons.
        /// </summary>
        public RelayCommand SidebarButtonCommand
        {
            get
            {
                if (m_sidebarButtonCommand == null)
                {
                    m_sidebarButtonCommand = new RelayCommand((Action)(() =>
                    {

                    }));
                }

                return m_sidebarButtonCommand;
            }
        }

        /// <summary>
        /// Command to run a deactivation request for the current authenticated user.
        /// </summary>
        public RelayCommand RequestDeactivateCommand
        {
            get
            {
                if (m_deactivationCommand == null)
                {
                    m_deactivationCommand = new RelayCommand((Action)(() =>
                    {
                        try
                        {
                            Task.Run(() =>
                            {
                                using (var ipcClient = new IPCClient())
                                {
                                    ipcClient.ConnectedToServer = () =>
                                    {
                                        ipcClient.RequestDeactivation();
                                    };

                                    ipcClient.WaitForConnection();
                                    Task.Delay(3000).Wait();
                                }
                            });
                        }
                        catch (Exception e)
                        {
                            LoggerUtil.RecursivelyLogException(m_logger, e);
                        }
                    }));
                }

                return m_deactivationCommand;
            }
        }

        /// <summary>
        /// Command to request the review of a logged block action.
        /// </summary>
        public RelayCommand<ViewableBlockedRequests> RequestBlockActionReviewCommand
        {
            get
            {
                if (m_deactivationCommand == null)
                {

                    m_requestBlockActionReviewCommand = new RelayCommand<ViewableBlockedRequests>((Action<ViewableBlockedRequests>)((args) =>
                    {
                        string category = args.CategoryName;
                        string fullUrl = args.FullRequest;

                        try
                        {
                            Task.Run(() =>
                            {
                                using (var ipcClient = new IPCClient())
                                {
                                    ipcClient.ConnectedToServer = () =>
                                    {
                                        ipcClient.RequestBlockActionReview(category, fullUrl);
                                    };

                                    ipcClient.WaitForConnection();
                                    Task.Delay(3000).Wait();
                                }
                            });
                        }
                        catch (Exception e)
                        {
                            LoggerUtil.RecursivelyLogException(m_logger, e);
                        }
                    }));
                }

                return m_requestBlockActionReviewCommand;
            }
        }
    }
}
