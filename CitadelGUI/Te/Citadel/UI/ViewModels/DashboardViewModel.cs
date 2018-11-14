/*
* Copyright © 2017 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Citadel.Core.Windows.Util;
using Citadel.IPC;
using CloudVeil.Windows;
using Filter.Platform.Common.Types;
using Filter.Platform.Common.Util;
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
using Te.Citadel.Testing;
using Te.Citadel.UI.Models;
using Te.Citadel.UI.Views;
using Te.Citadel.Util;

namespace Te.Citadel.UI.ViewModels
{ 

    public class DashboardViewModel : BaseCitadelViewModel
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

        public DashboardViewModel()
        {
        }

        internal DashboardModel Model
        {
            get
            {
                return m_model;
            }
        }


    }
}