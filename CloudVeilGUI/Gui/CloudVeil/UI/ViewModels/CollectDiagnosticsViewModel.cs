using GalaSoft.MvvmLight.CommandWpf;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Gui.CloudVeil.UI.Views;

namespace Gui.CloudVeil.UI.ViewModels
{
    public class CollectDiagnosticsViewModel : BaseCloudVeilViewModel
    {
        private RelayCommand saveToFileCommand;
        public RelayCommand SaveToFileCommand
        {
            get
            {
                if(saveToFileCommand == null)
                {
                    saveToFileCommand = new RelayCommand(() =>
                    {
                        SaveFileDialog dialog = new SaveFileDialog();

                        dialog.Filter = "Text Files|*.txt";
                        dialog.Title = "Save Report";

                        bool? result = dialog.ShowDialog();
                        if (result == true)
                        {
                            string text = DiagnosticsText; // Prevent cross-thread errors by creating a new variable.
                            Task.Run(() =>
                            {
                                try
                                {
                                    File.WriteAllText(dialog.FileName, text);
                                    MessageBox.Show("Saved the computer information file to the location you specified.");
                                }
                                catch(Exception ex)
                                {
                                    MessageBox.Show("Could not save the computer information file to the location you specified. Please try again.");
                                    logger.Error($"Could not save the computer info file: {ex}");
                                }
                            });
                        }
                    });
                }

                return saveToFileCommand;
            }
        }

        private RelayCommand closeCommand;
        public RelayCommand CloseCommand
        {
            get
            {
                if(closeCommand == null)
                {
                    closeCommand = new RelayCommand(() =>
                    {
                        ViewManager.PopView(typeof(CollectDiagnosticsView));
                    });
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
