using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Te.Citadel.Extensions;
using Te.Citadel.UI.Models;
using Te.Citadel.UI.Views;
using Te.Citadel.Util;

namespace Te.Citadel.UI.ViewModels
{
    public class DashboardViewModel : BaseCitadelViewModel
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
            BlockEvents = new ObservableCollection<ViewableBlockedRequests>();
        }

        /// <summary>
        /// Private data member for the public DeactivateCommand property.
        /// </summary>
        private RelayCommand m_deactivationCommand;

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

        /// <summary>
        /// Command to run a deactivation request for the current authenticated user.
        /// </summary>
        public RelayCommand RequestDeactivateCommand
        {
            get
            {
                if(m_deactivationCommand == null)
                {
                    m_deactivationCommand = new RelayCommand((Action)(async () =>
                    {
                        try
                        {
                            RequestViewChangeCommand.Execute(typeof(ProgressWait));
                            var authSuccess = await m_model.RequestAppDeactivation();

                            if(!authSuccess)
                            {
                                RequestViewChangeCommand.Execute(typeof(DashboardView));
                            }
                            else
                            {
                                if(ProcessProtection.IsProtected)
                                {
                                    ProcessProtection.Unprotect();
                                }

                                // Init the shutdown of this application.
                                Application.Current.Shutdown(ExitCodes.ShutdownWithoutSafeguards);
                                return;
                            }
                        }
                        catch(Exception e)
                        {
                            LoggerUtil.RecursivelyLogException(m_logger, e);
                        }
                    }));
                }

                return m_deactivationCommand;
            }
        }

        public RelayCommand ViewLogsCommand
        {
            get
            {
                if(m_viewLogsCommand == null)
                {
                    m_viewLogsCommand = new RelayCommand(() =>
                    {
                        // Scan all Nlog log targets
                        var logDir = string.Empty;

                        var targets = NLog.LogManager.Configuration.AllTargets;

                        foreach(var target in targets)
                        {
                            if(target is NLog.Targets.FileTarget)
                            {
                                var fTarget = (NLog.Targets.FileTarget)target;
                                var logEventInfo = new NLog.LogEventInfo { TimeStamp = DateTime.Now };
                                var fName = fTarget.FileName.Render(logEventInfo);

                                if(!string.IsNullOrEmpty(fName) && !string.IsNullOrWhiteSpace(fName))
                                {
                                    logDir = Directory.GetParent(fName).FullName;
                                    break;
                                }
                            }
                        }

                        if(string.IsNullOrEmpty(logDir) || string.IsNullOrWhiteSpace(logDir))
                        {
                            // Fallback, just in case.
                            logDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                        }

                        // Call process start with the dir path, explorer will handle it.
                        Process.Start(logDir);
                    });
                }

                return m_viewLogsCommand;
            }
        }

        public RelayCommand UseRelaxedPolicyCommand
        {
            get
            {
                if(m_useRelaxedPolicyCommand == null)
                {
                    m_useRelaxedPolicyCommand = new RelayCommand(() =>
                    {
                        m_model.RequestRelaxedPolicy();
                    }, () => AvailableRelaxedRequests > 0);
                }

                return m_useRelaxedPolicyCommand;
            }
        }

        public RelayCommand RelinquishRelaxedPolicyCommand
        {
            get
            {
                if(m_relinquishRelaxedPolicyCommand == null)
                {
                    m_relinquishRelaxedPolicyCommand = new RelayCommand(() =>
                    {
                        m_model.RelinquishRelaxedPolicy();
                    }, () => true);
                }

                return m_relinquishRelaxedPolicyCommand;
            }
        }

        public int AvailableRelaxedRequests
        {
            get
            {
                return m_model.AvailableRelaxedRequests;
            }

            set
            {
                m_model.AvailableRelaxedRequests = value;
                RaisePropertyChanged(nameof(AvailableRelaxedRequests));
            }
        }

        public string RelaxedDuration
        {
            get
            {
                return m_model.RelaxedDuration;
            }

            set
            {
                m_model.RelaxedDuration = value;
                RaisePropertyChanged(nameof(RelaxedDuration));
            }
        }
    }
}