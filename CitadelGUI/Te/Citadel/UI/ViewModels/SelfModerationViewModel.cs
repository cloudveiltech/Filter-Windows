/*
* Copyright © 2019 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/
using GalaSoft.MvvmLight.CommandWpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Te.Citadel.UI.ViewModels
{
    public class SelfModerationViewModel : BaseCitadelViewModel
    {
        private string newSelfModerationSite;
        public string NewSelfModerationSite
        {
            get
            {
                return newSelfModerationSite;
            }

            set
            {
                newSelfModerationSite = value;
                RaisePropertyChanged(nameof(NewSelfModerationSite));
            }
        }

        private ObservableCollection<string> selfModeratedSites;
        public ObservableCollection<string> SelfModeratedSites
        {
            get
            {
                return selfModeratedSites;
            }

            set
            {
                selfModeratedSites = value;
                RaisePropertyChanged(nameof(SelfModeratedSites));
            }
        }

        private RelayCommand<string> addNewSiteCommand;
        public RelayCommand<string> AddNewSiteCommand
        {
            get
            {
                if(addNewSiteCommand == null)
                {
                    addNewSiteCommand = new RelayCommand<string>((site) =>
                    {
                        // TODO: Add m_ipcClient message.
                    });
                }

                return addNewSiteCommand;
            }
        }
    }
}
