/*
* Copyright © 2017 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Te.Citadel.UI.Models;

namespace Te.Citadel.UI.ViewModels
{
    public class MainWindowViewModel : BaseCitadelViewModel
    {
        private MainWindowModel m_model;

        public bool InternetIsConnected
        {
            get
            {
                return m_model.InternetIsConnected;
            }
        }

        public MainWindowViewModel()
        {
            m_model = new MainWindowModel();
            m_model.PropertyChanged += OnModelChange;
        }

        private void OnModelChange(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch(e.PropertyName)
            {
                case nameof(InternetIsConnected):
                    {
                        RaisePropertyChanged(nameof(InternetIsConnected));
                    }
                    break;
            }
        }
    }
}