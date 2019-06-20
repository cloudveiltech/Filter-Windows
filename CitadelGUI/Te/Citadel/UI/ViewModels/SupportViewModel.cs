using Citadel.IPC;
using CloudVeil.Windows;
using Filter.Platform.Common.Data.Models;
using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Te.Citadel.UI.Views;

namespace Te.Citadel.UI.ViewModels
{
    public class SupportViewModel : BaseCitadelViewModel
    {
        private string activationIdentifier;
        public string ActivationIdentifier
        {
            get => activationIdentifier;
            set
            {
                activationIdentifier = value;
                RaisePropertyChanged(nameof(ActivationIdentifier));
            }
        }

        private RelayCommand collectDiagnosticsCommand;
        public RelayCommand CollectDiagnosticsCommand
        {
            get
            {
                if (collectDiagnosticsCommand == null)
                {
                    collectDiagnosticsCommand = new RelayCommand(() =>
                    {
                        var app = (CitadelApp.Current as CitadelApp);
                        var vm = app.ModelManager.Get<CollectDiagnosticsViewModel>();

                        app.IpcClient.Request(IpcCall.CollectComputerInfo).OnReply((h, msg) =>
                        {
                            if (!(msg.DataObject is ComputerInfo))
                            {
                                throw new InvalidCastException("DataObject is not ComputerInfo like expected.");
                            }

                            var computerInfo = msg.DataObject as ComputerInfo;
                            app.Dispatcher.BeginInvoke(new Action(() => vm.DiagnosticsText = computerInfo.DiagnosticsText));

                            return true;
                        });

                        ViewManager.PushView(10, typeof(CollectDiagnosticsView));
                    });
                }

                return collectDiagnosticsCommand;
            }
        }

        private RelayCommand viewLogsCommand;
        public RelayCommand ViewLogsCommand
        {
            get
            {
                if (viewLogsCommand == null)
                {
                    viewLogsCommand = new RelayCommand(() =>
                    {
                        // Scan all Nlog log targets
                        var logDir = string.Empty;

                        var targets = NLog.LogManager.Configuration.AllTargets;

                        foreach (var target in targets)
                        {
                            if (target is NLog.Targets.FileTarget)
                            {
                                var fTarget = (NLog.Targets.FileTarget)target;
                                var logEventInfo = new NLog.LogEventInfo { TimeStamp = DateTime.Now };
                                var fName = fTarget.FileName.Render(logEventInfo);

                                if (!string.IsNullOrEmpty(fName) && !string.IsNullOrWhiteSpace(fName))
                                {
                                    logDir = Directory.GetParent(fName).FullName;
                                    break;
                                }
                            }
                        }

                        if (string.IsNullOrEmpty(logDir) || string.IsNullOrWhiteSpace(logDir))
                        {
                            // Fallback, just in case.
                            logDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                        }

                        // Call process start with the dir path, explorer will handle it.
                        Process.Start(logDir);
                    });
                }

                return viewLogsCommand;
            }
        }

    }
}
