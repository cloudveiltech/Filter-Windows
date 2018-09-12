/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Te.Citadel.Testing;
using Te.Citadel.UI.Windows;

namespace Te.Citadel.UI.ViewModels
{
    public class DiagnosticsEntryViewModel
    {
        public DiagnosticsEntryViewModel(MainWindow mainWindow, DiagnosticsEntry entry)
        {
            m_mainWindow = mainWindow;

            Entry = entry;
        }

        public DiagnosticsEntry Entry { get; set; }

        private MainWindow m_mainWindow;

        private RelayCommand m_viewTestDetails;
        public RelayCommand ViewTestDetails
        {
            get
            {
                if (m_viewTestDetails == null)
                {
                    m_viewTestDetails = new RelayCommand(() =>
                    {
                        m_mainWindow.ShowUserMessage("Test Details", Entry.Details == null ? "No details available." : Entry.Details);
                    });
                }

                return m_viewTestDetails;
            }
        }

        public string PassedText
        {
            get
            {
                return Entry.Passed ? "Passed" : "Failed";
            }
        }

        public Visibility PassedVisibility
        {
            get
            {
                return Entry.Passed ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public Visibility FailedVisibility
        {
            get
            {
                return Entry.Passed ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public string TestText
        {
            get
            {
                return Entry.Test.ToString();
            }
        }
    }
}
