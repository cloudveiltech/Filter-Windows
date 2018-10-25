using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudVeil.Windows.ViewModels
{
    public class AdvancedPageViewModel : ProxyViewModelBase<CloudVeilGUI.ViewModels.AdvancedPageViewModel>
    {
        public AdvancedPageViewModel(CloudVeilGUI.ViewModels.AdvancedPageViewModel viewModel) : base(viewModel)
        {

        }

        private WPFCommand deactivateCommand;
        public WPFCommand DeactivateCommand
        {
            get => deactivateCommand ?? (deactivateCommand = new WPFCommand(viewModel.DeactivateCommand));
        }

        private WPFCommand synchronizeSettingsCommand;
        public WPFCommand SynchronizeSettingsCommand
        {
            get => synchronizeSettingsCommand ?? (synchronizeSettingsCommand = new WPFCommand(viewModel.SynchronizeSettingsCommand));
        }

        private WPFCommand checkForUpdatesCommand;
        public WPFCommand CheckForUpdatesCommand
        {
            get => checkForUpdatesCommand ?? (checkForUpdatesCommand = new WPFCommand(viewModel.CheckForUpdatesCommand));
        }

        private WPFCommand viewCertErrorsCommand;
        public WPFCommand ViewCertErrorsCommand
        {
            get => viewCertErrorsCommand ?? (viewCertErrorsCommand = new WPFCommand(viewModel.ViewCertErrorsCommand));
        }
    }
}
