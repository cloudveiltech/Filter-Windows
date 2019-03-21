using CloudVeilInstallerUI.Models;
using CloudVeilInstallerUI.Views;
using Microsoft.Deployment.WindowsInstaller;
using Microsoft.Tools.WindowsInstallerXml.Bootstrapper;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Interop;
using CVInstallType = CloudVeilInstallerUI.Models.InstallType;

namespace CloudVeilInstallerUI.ViewModels
{
    public interface ISetupUI
    {
        void ShowWelcome();
        void ShowInstall();

        void ShowFinish();

        void Show();
        void Close();

        event EventHandler Closed;

        IntPtr Hwnd { get; }
    }

    public interface IInstallerViewModel : INotifyPropertyChanged
    {
        void TriggerWelcome();
        void TriggerDetect();
        void TriggerInstall();
        void TriggerFailed(string message, string heading = null);
        void TriggerFinished();
        void Exit();

        string WelcomeButtonText { get; set; }

        string WelcomeHeader { get; set; }

        string WelcomeText { get; set; }

        bool ShowPrompts { get; set; }

        CVInstallType InstallType { get; set; }

        InstallationState State { get; set; }

        string Description { get; set; }

        bool IsIndeterminate { get; set; }

        int Progress { get; set; }

        string FinishedHeading { get; set; }

        string FinishedMessage { get; set; }

        string FinishButtonText { get; set; }
    }

    public class InstallerViewModel : IInstallerViewModel
    {
        public InstallerViewModel(BootstrapperApplication ba)
        {
            this.ba = ba;

            PropertyChanged += InstallerViewModel_PropertyChanged;
            ba.DetectBegin += DetectBegin;
            ba.DetectPackageComplete += DetectedPackage;
            ba.DetectRelatedMsiPackage += DetectRelatedPackage;
            ba.DetectRelatedBundle += DetectRelatedBundle;
            ba.DetectComplete += DetectComplete;

            ba.PlanPackageBegin += PlanPackageBegin;
            ba.PlanRelatedBundle += PlanRelatedBundle;

            ba.PlanComplete += PlanComplete;

            ba.ApplyBegin += ApplyBegin;
            ba.Progress += OnProgress;
            ba.ExecuteProgress += OnExecuteProgress;

            ba.ExecutePackageComplete += OnExecutePackageComplete;
            ba.ApplyComplete += ApplyComplete;
            
            ba.ResolveSource += ResolveSource;
        }

        private BootstrapperApplication ba;
        private IntPtr hwnd;

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public ISetupUI SetupUi { get; set; }

        public void SetSetupUi(ISetupUI ui)
        {
            SetupUi = ui;
            hwnd = ui.Hwnd;
        }

        public void TriggerWelcome()
        {
            try
            {
                SetupUi.ShowWelcome();
            }
            catch (Exception ex)
            {
                ba.Engine.Log(LogLevel.Error, ex.Message + " " + ex.ToString());
                throw ex;
            }
        }

        public void TriggerDetect()
        {
            ba.Engine.Detect(hwnd);
        }

        public void TriggerInstall()
        {
            SetupUi.ShowInstall();
            State = InstallationState.Initializing;

            LaunchAction desiredPlan = LaunchAction.Install;
            switch(InstallType)
            {
                case CVInstallType.NewInstall:
                    desiredPlan = LaunchAction.Install;
                    break;

                case CVInstallType.Uninstall:
                    desiredPlan = LaunchAction.Uninstall;
                    break;

                case CVInstallType.Update:
                    desiredPlan = LaunchAction.UpdateReplace;
                    break;
            }

            ba.Engine.Plan(desiredPlan);

            // TODO: Set up planning here.
            // Then apply.
        }

        public void TriggerFailed(string message, string heading = null)
        {
            if (heading == null) heading = $"Failed to {installTypeVerb} CloudVeil for Windows";

            State = InstallationState.Failed;
            FinishedMessage = message;
            FinishedHeading = heading;
            FinishButtonText = "Exit";

            SetupUi.ShowFinish();
        }

        public void TriggerFinished()
        {
            State = InstallationState.Installed;
            string verb = "has been " + installTypeVerbPast;

            FinishedMessage = $"CloudVeil for Windows {verb} successfully.";
            FinishedHeading = $"CloudVeil for Windows";
            FinishButtonText = $"Finish";

            SetupUi.ShowFinish();
        }

        public void Exit()
        {
            SetupUi.Close();
        }

        #region Variables
        private string welcomeButtonText = "Next";
        public string WelcomeButtonText
        {
            get => welcomeButtonText;
            set
            {
                welcomeButtonText = value;
                OnPropertyChanged(nameof(WelcomeButtonText));
            }
        }

        private string welcomeHeader = "Welcome to CloudVeil for Windows";
        public string WelcomeHeader
        {
            get => welcomeHeader;
            set
            {
                welcomeHeader = value;
                OnPropertyChanged(nameof(WelcomeHeader));
            }
        }

        private string welcomeText = null;
        public string WelcomeText
        {
            get => welcomeText;
            set
            {
                welcomeText = value;
                OnPropertyChanged(nameof(WelcomeText));
            }
        }

        private bool showPrompts = true;
        public bool ShowPrompts
        {
            get => showPrompts;
            set
            {
                showPrompts = value;
                OnPropertyChanged(nameof(ShowPrompts));
            }
        }

        private CVInstallType installType;
        public CVInstallType InstallType
        {
            get => installType;
            set
            {
                installType = value;
                OnPropertyChanged(nameof(InstallType));
            }
        }

        private InstallationState state;
        public InstallationState State
        {
            get => state;
            set
            {
                state = value; OnPropertyChanged(nameof(State));
            }
        }

        private string description;
        public string Description
        {
            get => description;
            set
            {
                description = value;
                OnPropertyChanged(nameof(Description));
            }
        }

        private bool isIndeterminate;
        public bool IsIndeterminate
        {
            get => isIndeterminate;
            set
            {
                isIndeterminate = value;
                OnPropertyChanged(nameof(IsIndeterminate));
            }
        }

        private int progress;
        public int Progress
        {
            get => progress;
            set
            {
                progress = value;
                OnPropertyChanged(nameof(Progress));
            }
        }

        private string finishedHeading;
        public string FinishedHeading
        {
            get => finishedHeading;
            set
            {
                finishedHeading = value;
                OnPropertyChanged(nameof(FinishedHeading));
            }
        }

        private string finishedMessage;
        public string FinishedMessage
        {
            get => finishedMessage;
            set
            {
                finishedMessage = value;
                OnPropertyChanged(nameof(FinishedMessage));
            }
        }

        private string finishButtonText;
        public string FinishButtonText
        {
            get => finishButtonText;
            set
            {
                finishButtonText = value;
                OnPropertyChanged(nameof(FinishButtonText));
            }
        }

        #endregion

        #region Installer Callbacks
        private void DetectBegin(object sender, DetectBeginEventArgs e)
        {
            State = InstallationState.Initializing;
        }

        private void DetectComplete(object sender, DetectCompleteEventArgs e)
        {
            if(ShowPrompts)
            {
                this.TriggerWelcome();
            }
            else
            {
                this.TriggerInstall();
            }
        }

        private void DetectedPackage(object sender, DetectPackageCompleteEventArgs e)
        {
            if(e.PackageId == "CloudVeilForWindows")
            {
                if (e.State == PackageState.Absent && InstallType == CVInstallType.None)
                {
                    InstallType = CVInstallType.NewInstall;
                }
                else if (e.State == PackageState.Present && InstallType == CVInstallType.None)
                {
                    InstallType = CVInstallType.Uninstall;
                }
            }
        }

        private void DetectRelatedPackage(object sender, DetectRelatedMsiPackageEventArgs e)
        {
            if(e.Operation == RelatedOperation.MajorUpgrade)
            {
                var installedPackage = new ProductInstallation(e.ProductCode);

                // TODO: Use https://stackoverflow.com/questions/17552989/how-do-i-detect-the-currently-installed-features-during-a-majorupgrade-using-wix
                // TODO: Set plan features.

                InstallType = CVInstallType.Update;
            }
        }

        private Dictionary<string, RequestState> relatedBundles = new Dictionary<string, RequestState>();
        private void DetectRelatedBundle(object sender, DetectRelatedBundleEventArgs e)
        {
            ba.Engine.Log(LogLevel.Standard, e.ProductCode);

            if (e.RelationType == RelationType.Update || e.RelationType == RelationType.Upgrade)
            {
                relatedBundles.Add(e.ProductCode, RequestState.Absent);
            }
        }

        private void PlanPackageBegin(object sender, PlanPackageBeginEventArgs e)
        {
            // TODO?
            if(e.PackageId.Equals("NetFx462Web"))
            {
                e.State = RequestState.None;
            }
        }


        private void PlanRelatedBundle(object sender, PlanRelatedBundleEventArgs e)
        {
            RequestState state;
            if(relatedBundles.TryGetValue(e.BundleId, out state))
            {
                e.State = state;
            }
        }

        private void PlanComplete(object sender, PlanCompleteEventArgs e)
        {
            ba.Engine.Log(LogLevel.Standard, "PlanComplete - " + e.Status);

            if(e.Status >= 0)
            {
                ba.Engine.Log(LogLevel.Standard, "Should start executing apply");

                this.State = InstallationState.Installing;
                try
                {
                    ba.Engine.Apply(hwnd);
                }
                catch(Exception ex)
                {
                    ba.Engine.Log(LogLevel.Error, "Attempted apply call. " + ex.ToString());
                }
            }
            else
            {
                State = InstallationState.Failed;
                TriggerFailed($"Failed to plan package install with error number {e.Status}");
            }
        }

        private void ResolveSource(object sender, ResolveSourceEventArgs e)
        {
            
        }

        private void OnProgress(object sender, ProgressEventArgs e)
        {
            //ba.Engine.Log(LogLevel.Standard, $"BundleProgress: {e.OverallPercentage} - {e.ProgressPercentage}");

            //this.Progress = e.OverallPercentage;
            this.IsIndeterminate = false;
        }

        private void OnExecuteProgress(object sender, ExecuteProgressEventArgs e)
        {
            ba.Engine.Log(LogLevel.Standard, $"ExecuteProgress: {e.PackageId} - {e.OverallPercentage} - {e.ProgressPercentage}");
            this.Progress = e.OverallPercentage;
            this.IsIndeterminate = false;
        }

        private string failedPackageId = null;
        private void OnExecutePackageComplete(object sender, ExecutePackageCompleteEventArgs e)
        {
            if(e.Status < 0)
            {
                failedPackageId = e.PackageId;
            }
        }


        private void ApplyBegin(object sender, ApplyBeginEventArgs e)
        {
            ba.Engine.Log(LogLevel.Standard, "ApplyBegin");
        }

        private void ApplyComplete(object sender, ApplyCompleteEventArgs e)
        {
            if (e.Status >= 0)
            {
                TriggerFinished();
            }
            else
            {
                string message = null;
                if(installType == CVInstallType.Uninstall && failedPackageId == "CloudVeilForWindows")
                {
                    message = $"InstallGuard has blocked removal of CloudVeil for Windows. Please deactivate the filter before trying again.";
                    // TODO: We need to be able to check this stuff for certain.
                }
                else if(e.Status == ApplyStatus.FAIL_NOACTION_REBOOT)
                {
                    message = $"Failed to {installTypeVerb} CloudVeil for Windows because a reboot is required.";
                }
                else
                {
                    message = $"Failed to {installTypeVerb} CloudVeil for Windows with error code {e.Status}. Please try again or contact support.";
                }

                TriggerFailed(message);
            }
        }

        #endregion

        private void OnStateChange()
        {
            if(State == InstallationState.Initializing)
            {
                Description = "Initializing...";
                IsIndeterminate = true;
            }
            else if(State == InstallationState.Installing)
            {
                Description = (installType == CVInstallType.Uninstall) ? "Removing..." : "Installing...";
                IsIndeterminate = false;
            }
            else if(State == InstallationState.Installed)
            {
                Description = (installType == CVInstallType.Uninstall) ? "Removed" : "Installed";
                IsIndeterminate = false;
            }
        }

        private string installTypeVerb = null;
        private string installTypeVerbPast = null;

        private void OnInstallTypeChange()
        {
            switch(InstallType)
            {
                case CVInstallType.NewInstall:
                    installTypeVerb = "install";
                    installTypeVerbPast = "installed";

                    WelcomeButtonText = "Install";
                    WelcomeHeader = "Welcome to CloudVeil for Windows";
                    WelcomeText = "Whenever you're ready, just click the 'install' button to install CloudVeil for Windows.";

                    break;

                case CVInstallType.Uninstall:
                    installTypeVerb = "remove";
                    installTypeVerbPast = "removed";

                    WelcomeHeader = "Removing CloudVeil for Windows";
                    WelcomeButtonText = "Remove";
                    WelcomeText = "To remove CloudVeil for Windows, first make sure that you've requested a deactivation\r\nand that your filter is no longer running.\r\n\r\nWhen ready, click 'remove' to uninstall CloudVeil for Windows.";
                    // TODO: This is where we need to check for a running service, and then check for a deactivation request.

                    break;

                case CVInstallType.Update:
                    installTypeVerb = "update";
                    installTypeVerbPast = "updated";

                    WelcomeButtonText = "Update";
                    WelcomeHeader = "Time to Update!";
                    WelcomeText = "Whenever you're ready, just click the 'update' button, and we'll get you updated to the latest version of CloudVeil for Windows.";
                    break;
            }
        }

        private void InstallerViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(State))
            {
                OnStateChange();
            }
            else if (e.PropertyName == nameof(InstallType))
            {
                OnInstallTypeChange();
            }
        }
    }
}
