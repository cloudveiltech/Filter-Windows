using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Te.Citadel.Testing;
using Te.Citadel.UI.Windows;

namespace Te.Citadel.UI.ViewModels
{
    public class DiagnosticsEntryViewModel
    {
        public DiagnosticsEntryViewModel(MainWindow mainWindow, DiagnosticsEntry entry)
        {
            m_mainWindow = mainWindow;

            Entry = entry;
        }

        public DiagnosticsEntry Entry { get; set; }

        private MainWindow m_mainWindow;

        private RelayCommand m_viewTestDetails;
        public RelayCommand ViewTestDetails
        {
            get
            {
                if (m_viewTestDetails == null)
                {
                    m_viewTestDetails = new RelayCommand(() =>
                    {
                        m_mainWindow.ShowUserMessage("Test Details", Entry.Details == null ? "No details available." : Entry.Details);
                    });
                }

                return m_viewTestDetails;
            }
        }

        public string PassedText
        {
            get
            {
                return Entry.Passed ? "Passed" : "Failed";
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
