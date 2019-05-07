using CloudVeilInstallerUI.IPC;
using CloudVeilInstallerUI.Models;
using CloudVeilInstallerUI.Views;
using Microsoft.Deployment.WindowsInstaller;
using Microsoft.Tools.WindowsInstallerXml.Bootstrapper;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Threading;
using CVInstallType = CloudVeilInstallerUI.Models.InstallType;

namespace CloudVeilInstallerUI.ViewModels
{
    public interface ISetupUI
    {
        void ShowWelcome();
        void ShowLicense();
        void ShowInstall();

        void ShowFinish();

        void Show();
        void Close();

        event EventHandler Closed;

        IntPtr Hwnd { get; }
        Dispatcher Dispatcher { get; }

    }

    public interface IInstallerViewModel : INotifyPropertyChanged
    {
        void TriggerWelcome();
        void TriggerLicense();
        void TriggerInstall();
        void TriggerFailed(string message, string heading = null, bool needsRestart = false);
        void TriggerFinished();
        void Exit();

        void StartFilterIfExists();

        string WelcomeButtonText { get; set; }

        string WelcomeHeader { get; set; }

        string WelcomeText { get; set; }

        bool ShowPrompts { get; set; }

        bool NeedsRestart { get; set; }

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
        public InstallerViewModel(CloudVeilBootstrapper ba)
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

        private CloudVeilBootstrapper ba;
        private IntPtr hwnd;

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public ISetupUI SetupUi { get; set; }

        bool isOlderVersionThanInstalled = false;

        public void SetSetupUi(ISetupUI ui)
        {
            SetupUi = ui;
            hwnd = ui.Hwnd;
        }

        public void TriggerWelcome()
        {
            ba.Engine.Log(LogLevel.Standard, $"TriggerWelcome {ba.Command.Display}");

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

        public void TriggerLicense()
        {
            SetupUi.ShowLicense();
        }

        public void TriggerInstall()
        {
            ba.Engine.Log(LogLevel.Standard, $"TriggerInstall {ba.Command.Display}");

            if(ba.Command.Display != Display.None && ba.Command.Display != Display.Embedded)
            {
                SetupUi.ShowInstall();
            }

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
                    desiredPlan = LaunchAction.Install;
                    break;
            }

            ba.Engine.Plan(desiredPlan);
        }

        public void TriggerFailed(string message, string heading = null, bool needsRestart = false)
        {
            try
            {
                ba.Engine.Log(LogLevel.Standard, $"TriggerFailed {ba.Command.Display}");

                switch (ba.Command.Display)
                {
                    case Display.Full:
                    case Display.Passive:
                        break;

                    default:
                        Exit();
                        return;
                }

                if (heading == null) heading = $"Failed to {installTypeVerb} CloudVeil for Windows";

                State = InstallationState.Failed;
                FinishedMessage = message;
                FinishedHeading = heading;
                FinishButtonText = "Exit";

                SetupUi.ShowFinish();
            }
            finally
            {
                try
                {
                    StartFilterIfExists();
                }
                catch(Exception ex)
                {
                    ba.Engine.Log(LogLevel.Error, $"Error occurred while restarting filter. {ex}");
                }
            }
        }

        public void TriggerFinished()
        {
            ba.Engine.Log(LogLevel.Standard, $"TriggerFinished {ba.Command.Display}");

            switch (ba.Command.Display)
            {
                case Display.Full:
                    break;

                default:
                    Exit();
                    return;
            }

            State = InstallationState.Installed;
            string verb = "has been " + installTypeVerbPast;

            FinishedMessage = $"CloudVeil for Windows {verb} successfully.";
            FinishedHeading = $"CloudVeil for Windows";
            FinishButtonText = $"Finish";

            SetupUi.ShowFinish();
        }

        public void Exit()
        {
            ba.Engine.Log(LogLevel.Standard, "Exit called");

            ba.IsExiting = true;

            Delegate closeMe = new Action(() => SetupUi.Close());

            if (SetupUi.Dispatcher != null)
            {
                SetupUi.Dispatcher.BeginInvoke(closeMe);
            }
            else
            {
                SetupUi.Close();
            }
        }

        public void StartFilterIfExists()
        {
            Services services = new Services(ba);
            if(services.Exists("FilterServiceProvider"))
            {
                services.Start("FilterServiceProvider");
            }
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

        private bool needsRestart = false;
        public bool NeedsRestart
        {
            get => needsRestart;
            set
            {
                needsRestart = value;
                OnPropertyChanged(nameof(NeedsRestart));
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

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// If <see cref="detectedNewerBundle"/> is true and <see cref="installedMsiNonexistent"/> is false,
        /// then we want to trigger removal of this bundle without surfacing any UI.
        /// </remarks>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DetectComplete(object sender, DetectCompleteEventArgs e)
        {
            if(isOlderVersionThanInstalled)
            {
                TriggerFailed("A newer version of CloudVeil for Windows is already installed on this system. Please uninstall that version before installing this.", "Newer Version Installed");
                return;
            }

            LaunchAction desiredPlan = ba.Command.Action;

            switch (ba.Command.Display)
            {
                case Display.Full:
                    TriggerWelcome();
                    break;

                default:
                    TriggerInstall();
                    break;
            }
        }

        private void DetectedPackage(object sender, DetectPackageCompleteEventArgs e)
        {
            if(e.PackageId == "CloudVeilForWindows")
            {
                if(ba.Command.Action == LaunchAction.Uninstall)
                {
                    InstallType = CVInstallType.Uninstall;
                }
                else if (e.State == PackageState.Absent && InstallType == CVInstallType.None)
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
            Version myVersion = null;
            if(ba.Engine.VersionVariables.Contains("WixBundleVersion"))
            {
                myVersion = ba.Engine.VersionVariables["WixBundleVersion"];
            }

            Version otherVersion = e.Version;

            if(myVersion < otherVersion)
            {
                isOlderVersionThanInstalled = true;
            }

            if (e.Operation == RelatedOperation.MajorUpgrade)
            {
                var installedPackage = new ProductInstallation(e.ProductCode);

                // TODO: Use https://stackoverflow.com/questions/17552989/how-do-i-detect-the-currently-installed-features-during-a-majorupgrade-using-wix
                // TODO: Set plan features.

                InstallType = CVInstallType.Update;
            }
        }

        private Dictionary<string, Tuple<RequestState, DetectRelatedBundleEventArgs>> relatedBundles = new Dictionary<string, Tuple<RequestState, DetectRelatedBundleEventArgs>>();
        private void DetectRelatedBundle(object sender, DetectRelatedBundleEventArgs e)
        {
            ba.Engine.Log(LogLevel.Standard, e.ProductCode);

            RequestState defaultBundleState = ba.Command.Action == LaunchAction.Uninstall ? RequestState.None : RequestState.Absent;

            if (e.RelationType == RelationType.Update || e.RelationType == RelationType.Upgrade)
            {
                relatedBundles.Add(e.ProductCode, new Tuple<RequestState, DetectRelatedBundleEventArgs>(defaultBundleState, e));
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
            Tuple<RequestState, DetectRelatedBundleEventArgs> stateTuple;
            if(relatedBundles.TryGetValue(e.BundleId, out stateTuple))
            {
                e.State = stateTuple.Item1;
            }
        }

        private bool allFiltersHaveExited()
        {
            Process[] fsp = Process.GetProcessesByName("FilterServiceProvider");
            if (fsp.Length > 0)
            {
                // Check for HasExited, since the process may still be in the list, even though it has exited.
                try
                {
                    foreach (Process p in fsp)
                    {
                        if (!p.HasExited)
                        {
                            return false;
                        }
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    ba.Engine.Log(LogLevel.Standard, $"Warning: An exception occurred while waiting for filters to exit: {ex}");
                    return true; // If there is an exception while gleaning info from the processes in the list, just return true. We don't want to get stuck in a forever-waiting loop.
                }
            }
            else
            {
                return true;
            }
        }

        private async void executeOnFilterExited(Action action)
        {
            while (!allFiltersHaveExited())
            {
                await Task.Delay(100);
            }

            action?.Invoke();
        }

        private void PlanComplete(object sender, PlanCompleteEventArgs e)
        {
            if(e.Status >= 0)
            {
                this.State = InstallationState.Installing;
                try
                {
                    if(ba.WaitForFilterExit)
                    {
                        executeOnFilterExited(() => ba.Engine.Apply(hwnd));
                    }
                    else
                    {
                        ba.Engine.Apply(hwnd);
                    }
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
            this.Progress = e.OverallPercentage;
            this.IsIndeterminate = false;

            if(ba.Command.Display == Display.Embedded)
            {
                ba.Engine.SendEmbeddedProgress(e.ProgressPercentage, e.OverallPercentage);
            }
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

        int applyRetries = 3;

        private void ApplyComplete(object sender, ApplyCompleteEventArgs e)
        {
            if (e.Status >= 0)
            {
                TriggerFinished();
            }
            else
            {
                string message = null;
                bool needsRestart = false;

                if(installType == CVInstallType.Uninstall && failedPackageId == "CloudVeilForWindows")
                {
                    message = $"InstallGuard has blocked removal of CloudVeil for Windows. Please deactivate the filter before trying again.";
                    // TODO: We need to be able to check this stuff for certain.
                }
                else if(e.Status == ApplyStatus.FAIL_NOACTION_REBOOT)
                {
                    message = $"Failed to {installTypeVerb} CloudVeil for Windows because a reboot is required.";
                    needsRestart = true;
                }
                else if(e.Status == ApplyStatus.FAIL_PIPE_NO_DATA && applyRetries > 0)
                {
                    // Handle pipe errors introduced by our lovely friend the antivirus.
                    ba.Engine.Apply(hwnd);
                    applyRetries -= 1;
                    return;
                }
                else
                {
                    message = $"Failed to {installTypeVerb} CloudVeil for Windows with error code {e.Status}. Please restart your computer and try again. If the issue persists, please contact support.";
                }

                TriggerFailed(message, null, needsRestart);
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
