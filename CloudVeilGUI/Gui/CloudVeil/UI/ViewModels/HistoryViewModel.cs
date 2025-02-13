﻿/*
* Copyright © 2019 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/
using CloudVeil.Core.Windows.Util;
using CloudVeil.IPC;
using Filter.Platform.Common.Util;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Gui.CloudVeil.Extensions;
using Gui.CloudVeil.UI.Models;
using Gui.CloudVeil.UI.Views;
using Gui.CloudVeil.Util;

namespace Gui.CloudVeil.UI.ViewModels
{
    /// <summary>
    /// Class for displaying block event information in a DataGrid.
    /// </summary>
    public class ViewableBlockedRequest : ObservableObject
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

        public string BlockDate
        {
            get;
            private set;
        }

        public ViewableBlockedRequest(string category, string fullRequest, DateTime blockDate)
        {
            this.CategoryName = category;
            this.FullRequest = fullRequest;
            this.BlockDate = blockDate.ToString(CultureInfo.CurrentCulture);
        }
    }

    public class HistoryViewModel : BaseCloudVeilViewModel
    {


        /// <summary>
        /// The model.
        /// </summary>
        private DashboardModel model = new DashboardModel();

        /// <summary>
        /// List of observable block actions that the user can view.
        /// </summary>
        public ObservableCollection<ViewableBlockedRequest> BlockEvents
        {
            get;
            set;
        }

        public HistoryViewModel()
        {
            BlockEvents = new ObservableCollection<ViewableBlockedRequest>();
        }

        internal DashboardModel Model
        {
            get
            {
                return model;
            }
        }

        private ViewableBlockedRequest selectedItem;
        public ViewableBlockedRequest SelectedItem
        {
            get => selectedItem;
            set
            {
                selectedItem = value;
                RaisePropertyChanged(nameof(SelectedItem));
            }
        }

        private RelayCommand sidebarButtonCommand;

        /// <summary>
        /// Generic handler for all of the sidebar buttons.
        /// </summary>
        public RelayCommand SidebarButtonCommand
        {
            get
            {
                if (sidebarButtonCommand == null)
                {
                    sidebarButtonCommand = new RelayCommand((Action)(() =>
                    {

                    }));
                }

                return sidebarButtonCommand;
            }
        }

        private RelayCommand<ViewableBlockedRequest> copySelectedUrlCommand;
        public RelayCommand<ViewableBlockedRequest> CopySelectedUrlCommand
        {
            get
            {
                if(copySelectedUrlCommand == null)
                {
                    copySelectedUrlCommand = new RelayCommand<ViewableBlockedRequest>((args) =>
                    {
                        try
                        {
                            if (args == null) { return; }

                            string fullUrl = args.FullRequest;
                            Clipboard.SetText(fullUrl);
                        }
                        catch(Exception ex)
                        {
                            logger.Error(ex);
                        }
                    });
                }

                return copySelectedUrlCommand;
            }
        }

        private RelayCommand<ViewableBlockedRequest> requestBlockActionReviewCommand;

        /// <summary>
        /// Command to request the review of a logged block action.
        /// </summary>
        public RelayCommand<ViewableBlockedRequest> RequestBlockActionReviewCommand
        {
            get
            {
                if (requestBlockActionReviewCommand == null)
                {
                    requestBlockActionReviewCommand = new RelayCommand<ViewableBlockedRequest>((Action<ViewableBlockedRequest>)((args) =>
                    {
                        if (args == null) { return; }

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
                            LoggerUtil.RecursivelyLogException(logger, e);
                        }
                    }));
                }

                return requestBlockActionReviewCommand;
            }
        }

        public void AppendBlockActionEvent(string category, string fullRequest, DateTime blockDate)
        {
            try
            {
                var dataCtx = this;
                // Keep number of items truncated to 50.
                if (dataCtx.BlockEvents.Count > 50)
                {
                    dataCtx.BlockEvents.RemoveAt(0);
                }

                // Add the item to view.
                dataCtx.BlockEvents.Add(new ViewableBlockedRequest(category, fullRequest, blockDate));
            }
            catch (Exception e)
            {
                LoggerUtil.RecursivelyLogException(logger, e);
            }
        }
    }
}
