/*
* Copyright © 2017 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System.Collections.ObjectModel;
using Gui.CloudVeil.UI.Models;

namespace Gui.CloudVeil.UI.ViewModels
{

    public class DashboardViewModel : BaseCloudVeilViewModel
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

        public DashboardViewModel()
        {
        }

        internal DashboardModel Model
        {
            get
            {
                return model;
            }
        }


    }
}
