/*
* Copyright © 2017 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using GalaSoft.MvvmLight.CommandWpf;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using Gui.CloudVeil.UI.Models;

namespace Gui.CloudVeil.UI.ViewModels
{
    public class MainWindowViewModel : BaseCitadelViewModel
    {
        private MainWindowModel m_model;

        public bool InternetIsConnected => m_model.InternetIsConnected;

        public MahApps.Metro.IconPacks.PackIconFontAwesomeKind InternetIconKind
            => InternetIsConnected ? MahApps.Metro.IconPacks.PackIconFontAwesomeKind.CheckCircleSolid : MahApps.Metro.IconPacks.PackIconFontAwesomeKind.ExclamationCircleSolid;

        public string InternetToolTip => InternetIsConnected ? "Internet Connected" : "No Internet Connection";

        private bool m_isUserLoggedIn;
        public bool IsUserLoggedIn
        {
            get
            {
                return m_isUserLoggedIn;
            }

            set
            {
                m_isUserLoggedIn = value;
                RaisePropertyChanged(nameof(IsUserLoggedIn));
            }
        }

        private string m_loggedInUser;
        public string LoggedInUser
        {
            get
            {
                return m_loggedInUser;
            }

            set
            {
                m_loggedInUser = value;
                RaisePropertyChanged(nameof(LoggedInUser));
            }
        }

        private bool m_showGuestNetwork;

        public bool ShowIsGuestNetwork
        {
            get
            {
                return m_showGuestNetwork;
            }

            set
            {
                m_showGuestNetwork = value;
                RaisePropertyChanged(nameof(ShowIsGuestNetwork));
            }
        }

        private bool m_isCaptivePortalActive;

        /// <summary>
        /// If this is true, we show guest network window command.
        /// </summary>
        public bool IsCaptivePortalActive
        {
            get
            {
                return m_isCaptivePortalActive;
            }

            set
            {
                m_isCaptivePortalActive = value;
                RaisePropertyChanged(nameof(IsCaptivePortalActive));
            }
        }

        private bool downloadFlyoutIsOpen = false;
        public bool DownloadFlyoutIsOpen
        {
            get => downloadFlyoutIsOpen;
            set
            {
                downloadFlyoutIsOpen = value;
                RaisePropertyChanged(nameof(DownloadFlyoutIsOpen));
            }
        }

        private bool conflictsFlyoutIsOpen = false;
        public bool ConflictsFlyoutIsOpen
        {
            get => conflictsFlyoutIsOpen;
            set
            {
                conflictsFlyoutIsOpen = value;
                RaisePropertyChanged(nameof(ConflictsFlyoutIsOpen));
            }
        }

        private ObservableCollection<ConflictInfo> conflictReasons = new ObservableCollection<ConflictInfo>();

        public ObservableCollection<ConflictInfo> ConflictReasons
        {
            get => conflictReasons;
            set
            {
                conflictReasons = value;
                RaisePropertyChanged(nameof(ConflictReasons));
                RaisePropertyChanged(nameof(ConflictFlyoutButtonVisibility));
                RaisePropertyChanged(nameof(InverseConflictFlyoutButtonVisibility));
            }
        }

        public Visibility ConflictFlyoutButtonVisibility => conflictReasons?.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        public Visibility InverseConflictFlyoutButtonVisibility => ConflictFlyoutButtonVisibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

        private int downloadProgress;
        public int DownloadProgress
        {
            get => downloadProgress;
            set
            {
                downloadProgress = value;
                RaisePropertyChanged(nameof(DownloadProgress));
            }
        }

        private RelayCommand m_openGuestNetwork;
        
        public RelayCommand OpenGuestNetwork
        {
            get
            {
                if(m_openGuestNetwork == null)
                {
                    m_openGuestNetwork = new RelayCommand((Action)(() =>
                    {
                        ShowIsGuestNetwork = true;
                    }));
                }

                return m_openGuestNetwork;
            }
        }

        private RelayCommand m_openConflictsFlyout;
        public RelayCommand OpenConflictsFlyout
        {
            get
            {
                if(m_openConflictsFlyout == null)
                {
                    m_openConflictsFlyout = new RelayCommand(() =>
                    {
                        ConflictsFlyoutIsOpen = true;
                    });
                }

                return m_openConflictsFlyout;
            }
        }

        /*
        private RelayCommand ignoreConflicts;
        public RelayCommand IgnoreConflicts
        {
            get
            {
                if(ignoreConflicts == null)
                {
                    ignoreConflicts = new RelayCommand(() =>
                    {
                        // Do we actually want the ability to ignore conflicts?
                    });
                }
            }
        }*/

        public MainWindowViewModel()
        {
            m_model = new MainWindowModel();
            m_model.PropertyChanged += OnModelChange;

            conflictReasons.CollectionChanged += OnConflictReasonsChanged;
        }

        private void OnConflictReasonsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChanged(nameof(ConflictFlyoutButtonVisibility));
            RaisePropertyChanged(nameof(InverseConflictFlyoutButtonVisibility));
        }

        private void OnModelChange(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch(e.PropertyName)
            {
                case nameof(InternetIsConnected):
                    {
                        RaisePropertyChanged(nameof(InternetIsConnected));
                        RaisePropertyChanged(nameof(InternetIconKind));
                        RaisePropertyChanged(nameof(InternetToolTip));
                    }
                    break;
            }
        }
    }
}