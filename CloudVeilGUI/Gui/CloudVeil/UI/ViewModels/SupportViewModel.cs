using CloudVeil.IPC;
using CloudVeil.Windows;
using Filter.Platform.Common.Data.Models;
using Filter.Platform.Common.Util;
using GalaSoft.MvvmLight.Command;
using Gui.CloudVeil.UI.Views;
using Gui.CloudVeil.UI.Windows;
using Swan;
using System;
using System.Diagnostics;

namespace Gui.CloudVeil.UI.ViewModels
{
    public class SupportViewModel : BaseCloudVeilViewModel
    {
        private MainWindow mainWindow;

        public SupportViewModel(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;

        }
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
                        var app = (CloudVeilApp.Current as CloudVeilApp);
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
                        var app = (CloudVeilApp.Current as CloudVeilApp);
                        app.IpcClient.Request(IpcCall.DumpSystemEventLog);
                        // Call process start with the dir path, explorer will handle it.
                        Process.Start(logDir);
                    });
                }

                return viewLogsCommand;
            }
        }

        private RelayCommand sendLogsCommand;
        public RelayCommand SendLogsCommand
        {
            get
            {
                if (sendLogsCommand == null)
                {
                    sendLogsCommand = new RelayCommand(() =>
                    {
                        var app = (CloudVeilApp.Current as CloudVeilApp);
                        app.IpcClient.Request(IpcCall.SendEventLog).OnReply((h, msg) =>
                        {
                            mainWindow.Dispatcher.InvokeAsync(async () =>
                            {
                                var result = await mainWindow.AskUserYesNoQuestion("Sending logs", "Do you want to send the logs to CloudVeil support?");
                                if (result)
                                {
                                    if (msg.DataObject != null && msg.DataObject.ToBoolean())
                                    {
                                        mainWindow.ShowUserMessage("Logs Sent", "The logs have been sent to CloudVeil support. Thank you!");
                                    }
                                    else
                                    {
                                        mainWindow.ShowUserMessage("Logs Not Sent", "There was an error sending the logs to CloudVeil support. Please try again later.");
                                    }
                                }
                            });
                            
                            return true;
                        });
                    });
                }

                return sendLogsCommand;
            }
        }
    }
}
