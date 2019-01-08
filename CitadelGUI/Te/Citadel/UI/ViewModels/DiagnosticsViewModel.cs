/*
* Copyright © 2019 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/
using CloudVeil.Windows;
using GalaSoft.MvvmLight.CommandWpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Te.Citadel.Testing;

namespace Te.Citadel.UI.ViewModels
{
    public class DiagnosticsViewModel : BaseCitadelViewModel 
    {
        public DiagnosticsViewModel()
        {
            DiagnosticsEntries = new ObservableCollection<DiagnosticsEntryViewModel>();
        }

        private RelayCommand m_testFilterCommand;
        public RelayCommand TestFilterCommand
        {
            get
            {
                if (m_testFilterCommand == null)
                {
                    m_testFilterCommand = new RelayCommand(() =>
                    {
                        FilterTesting test = new FilterTesting();
                        test.OnFilterTestResult += Test_OnFilterTestResult;
                        DiagnosticsEntries.Clear();
                        testsPassed = 0;
                        testsTotal = 0;

                        Task.Run(() =>
                        {
                            test.TestFilter();
                        });
                    });
                }

                return m_testFilterCommand;
            }
        }

        private RelayCommand m_testDnsCommand;

        public RelayCommand TestDnsCommand
        {
            get
            {
                if (m_testDnsCommand == null)
                {
                    m_testDnsCommand = new RelayCommand(() =>
                    {
                        FilterTesting test = new FilterTesting();
                        test.OnFilterTestResult += Test_OnFilterTestResult;
                        DiagnosticsEntries.Clear();
                        testsPassed = 0;
                        testsTotal = 0;

                        Task.Run(() =>
                        {
                            test.TestDNS();
                        });
                    });
                }

                return m_testDnsCommand;
            }
        }

        private RelayCommand m_testSafeSearchCommand;

        public RelayCommand TestSafeSearchCommand
        {
            get
            {
                if (m_testSafeSearchCommand == null)
                {
                    m_testSafeSearchCommand = new RelayCommand(() =>
                    {
                        FilterTesting test = new FilterTesting();
                        test.OnFilterTestResult += Test_OnFilterTestResult;
                        DiagnosticsEntries.Clear();
                        testsPassed = 0;
                        testsTotal = 0;

                        Task.Run(() =>
                        {
                            test.TestDNSSafeSearch();
                        });
                    });
                }

                return m_testSafeSearchCommand;
            }
        }

        private int testsPassed = 0;
        private int testsTotal = 0;

        /// <summary>
        /// Used by filter test to propagate results back to the UI.
        /// </summary>
        /// <param name="test"></param>
        /// <param name="passed"></param>
        private void Test_OnFilterTestResult(DiagnosticsEntry entry)
        {
            // TODO: Build UI for this.
            m_logger.Info("OnFilterTestResult {0} {1}", entry.Test.ToString(), entry.Passed);
            if (entry.Exception != null)
            {
                m_logger.Error("OnFilterTestResult Exception: {0}", entry.Exception.ToString());
            }

            if (entry.Test == FilterTest.BlockingTest || entry.Test == FilterTest.DnsFilterTest)
            {
                CitadelApp.Current.Dispatcher.InvokeAsync(() =>
                {
                    (CitadelApp.Current.MainWindow as Windows.MainWindow).ShowUserMessage("Test Details", entry.Details);
                });
            }

            if (entry.Test == FilterTest.AllTestsCompleted)
            {
                return;
            }

            CitadelApp.Current.Dispatcher.InvokeAsync(() =>
            {
                // FIXME: Don't do CitadelApp.Current.MainWindow as Windows.MainWindow, pass it instead.
                DiagnosticsEntries.Add(new DiagnosticsEntryViewModel(CitadelApp.Current.MainWindow as Windows.MainWindow, entry));
            });
        }

        private ObservableCollection<DiagnosticsEntryViewModel> m_diagnosticsEntries;
        public ObservableCollection<DiagnosticsEntryViewModel> DiagnosticsEntries
        {
            get
            {
                return m_diagnosticsEntries;
            }

            set
            {
                m_diagnosticsEntries = value;
                RaisePropertyChanged(nameof(DiagnosticsEntries));
            }
        }

        private string m_diagnosticsLog;
        public string DiagnosticsLog
        {
            get
            {
                return m_diagnosticsLog;
            }

            set
            {
                m_diagnosticsLog = value;
                RaisePropertyChanged(nameof(DiagnosticsLog));
            }
        }
    }
}
