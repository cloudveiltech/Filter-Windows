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
using System.Windows;
using Gui.CloudVeil.Testing;

namespace Gui.CloudVeil.UI.ViewModels
{
    public class DiagnosticsViewModel : BaseCloudVeilViewModel 
    {
        public DiagnosticsViewModel()
        {
            DiagnosticsEntries = new ObservableCollection<DiagnosticsEntryViewModel>();
        }

        private RelayCommand testFilterCommand;
        public RelayCommand TestFilterCommand
        {
            get
            {
                if (testFilterCommand == null)
                {
                    testFilterCommand = new RelayCommand(() =>
                    {
                        FilterTesting test = new FilterTesting();
                        test.OnFilterTestResult += Test_OnFilterTestResult;
                        DiagnosticsEntries.Clear();
                        Task.Run(() =>
                        {
                            test.TestFilter();
                        });
                    });
                }

                return testFilterCommand;
            }
        }

        private RelayCommand testDnsCommand;

        public RelayCommand TestDnsCommand
        {
            get
            {
                if (testDnsCommand == null)
                {
                    testDnsCommand = new RelayCommand(() =>
                    {
                        FilterTesting test = new FilterTesting();
                        test.OnFilterTestResult += Test_OnFilterTestResult;
                        DiagnosticsEntries.Clear();

                        Task.Run(() =>
                        {
                            test.TestDNS();
                        });
                    });
                }

                return testDnsCommand;
            }
        }

        private Visibility dnsTestButtonVisibility;
        public Visibility DnsTestButtonVisibility
        {
            get
            {
                return dnsTestButtonVisibility;
            }
            set
            {
                dnsTestButtonVisibility = value;
                RaisePropertyChanged(nameof(DnsTestButtonVisibility));
            }
        }

        public Boolean IsDnsEnforcementEnabled
        {
            get
            {
                return dnsTestButtonVisibility == Visibility.Visible;
            }
            set
            {
                if(value)
                {
                    DnsTestButtonVisibility = Visibility.Visible;
                } else
                {
                    DnsTestButtonVisibility = Visibility.Hidden;
                }
            }
        }

        private RelayCommand testSafeSearchCommand;

        public RelayCommand TestSafeSearchCommand
        {
            get
            {
                if (testSafeSearchCommand == null)
                {
                    testSafeSearchCommand = new RelayCommand(() =>
                    {
                        FilterTesting test = new FilterTesting();
                        test.OnFilterTestResult += Test_OnFilterTestResult;
                        DiagnosticsEntries.Clear();

                        if (IsDnsEnforcementEnabled)
                        {
                            Task.Run(() =>
                            {
                                test.TestDNSSafeSearch();
                            });
                        } else
                        {
                            Test_OnFilterTestResult(new DiagnosticsEntry(FilterTest.DnsFilterTest, false, "DNS Enforcement is Disabled"));
                        }
                    });
                }

                return testSafeSearchCommand;
            }
        }

        /// <summary>
        /// Used by filter test to propagate results back to the UI.
        /// </summary>
        /// <param name="test"></param>
        /// <param name="passed"></param>
        private void Test_OnFilterTestResult(DiagnosticsEntry entry)
        {
            // TODO: Build UI for this.
            logger.Info("OnFilterTestResult {0} {1}", entry.Test.ToString(), entry.Passed);
            if (entry.Exception != null)
            {
                logger.Error("OnFilterTestResult Exception: {0}", entry.Exception.ToString());
            }

            if (entry.Test == FilterTest.BlockingTest || entry.Test == FilterTest.DnsFilterTest)
            {
                CloudVeilApp.Current.Dispatcher.InvokeAsync(() =>
                {
                    (CloudVeilApp.Current.MainWindow as Windows.MainWindow).ShowUserMessage("Test Details", entry.Details);
                });
            }

            if (entry.Test == FilterTest.AllTestsCompleted)
            {
                return;
            }

            CloudVeilApp.Current.Dispatcher.InvokeAsync(() =>
            {
                DiagnosticsEntries.Add(new DiagnosticsEntryViewModel(CloudVeilApp.Current.MainWindow as Windows.MainWindow, entry));
            });
        }

        private ObservableCollection<DiagnosticsEntryViewModel> diagnosticsEntries;
        public ObservableCollection<DiagnosticsEntryViewModel> DiagnosticsEntries
        {
            get
            {
                return diagnosticsEntries;
            }

            set
            {
                diagnosticsEntries = value;
                RaisePropertyChanged(nameof(DiagnosticsEntries));
            }
        }

        private string diagnosticsLog;
        public string DiagnosticsLog
        {
            get
            {
                return diagnosticsLog;
            }

            set
            {
                diagnosticsLog = value;
                RaisePropertyChanged(nameof(DiagnosticsLog));
            }
        }
    }
}
