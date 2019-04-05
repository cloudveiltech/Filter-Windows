using GalaSoft.MvvmLight.CommandWpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Te.Citadel.UI.Views;

namespace Te.Citadel.UI.ViewModels
{
    public class CollectDiagnosticsViewModel : BaseCitadelViewModel
    {
        private RelayCommand closeCommand;
        public RelayCommand CloseCommand
        {
            get
            {
                if(closeCommand == null)
                {
                    ViewManager.PopView(typeof(CollectDiagnosticsView));
                }

                return closeCommand;
            }
        }

        private RelayCommand copyCommand;
        public RelayCommand CopyCommand
        {
            get
            {
                if(copyCommand == null)
                {
                    copyCommand = new RelayCommand(() =>
                    {
                        Clipboard.SetText(diagnosticsText);
                    });
                }

                return copyCommand;
            }
        }

        private string diagnosticsText;
        public string DiagnosticsText
        {
            get => diagnosticsText;
            set
            {
                diagnosticsText = value;
                RaisePropertyChanged(nameof(DiagnosticsText));
            }
        }
    }
}
