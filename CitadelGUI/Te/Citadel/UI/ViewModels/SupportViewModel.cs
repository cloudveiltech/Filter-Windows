using Citadel.IPC;
using CloudVeil.Windows;
using Filter.Platform.Common.Data.Models;
using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Te.Citadel.UI.Views;

namespace Te.Citadel.UI.ViewModels
{
    public class SupportViewModel : BaseCitadelViewModel
    {


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
    }
}
