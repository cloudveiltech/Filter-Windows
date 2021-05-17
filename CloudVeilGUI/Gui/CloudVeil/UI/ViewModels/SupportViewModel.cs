using CloudVeil.IPC;
using CloudVeil.Windows;
using Filter.Platform.Common.Data.Models;
using Filter.Platform.Common.Util;
using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gui.CloudVeil.UI.Views;

namespace Gui.CloudVeil.UI.ViewModels
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
                        var logDir = LoggerUtil.LogFolderPath;

                        //dump event log 
                        var app = (CitadelApp.Current as CitadelApp);
                        app.IpcClient.Request(IpcCall.DumpSystemEventLog);
                        // Call process start with the dir path, explorer will handle it.
                        Process.Start(logDir);
                    });
                }

                return viewLogsCommand;
            }
        }

    }
}
