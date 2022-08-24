using GalaSoft.MvvmLight.CommandWpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Gui.CloudVeil.Testing;
using Gui.CloudVeil.UI.Windows;

namespace Gui.CloudVeil.UI.ViewModels
{
    public class DiagnosticsEntryViewModel
    {
        public DiagnosticsEntryViewModel(MainWindow mainWindow, DiagnosticsEntry entry)
        {
            this.mainWindow = mainWindow;

            Entry = entry;
        }

        public DiagnosticsEntry Entry { get; set; }

        private MainWindow mainWindow;

        private RelayCommand viewTestDetails;
        public RelayCommand ViewTestDetails
        {
            get
            {
                if (viewTestDetails == null)
                {
                    viewTestDetails = new RelayCommand(() =>
                    {
                        mainWindow.ShowUserMessage("Test Details", Entry.Details == null ? "No details available." : Entry.Details);
                    });
                }

                return viewTestDetails;
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
