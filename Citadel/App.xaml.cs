using HtmlAgilityPack;
using Microsoft.Win32;
using Newtonsoft.Json;
using NLog;
using opennlp.tools.doccat;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Te.Citadel.Data.Models;
using Te.Citadel.Extensions;
using Te.Citadel.UI.Models;
using Te.Citadel.UI.ViewModels;
using Te.Citadel.UI.Views;
using Te.Citadel.UI.Windows;
using Te.Citadel.Util;
using Te.HttpFilteringEngine;

namespace Te.Citadel
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class CitadelApp : Application
    {

        private class FilterListEntry
        {
            private volatile bool m_isBypass = false;

            public byte CategoryId
            {
                get;
                set;
            }

            public string CategoryName
            {
                get;
                set;
            }

            public bool IsBypass
            {
                get
                {
                    return m_isBypass;
                }

                set
                {
                    m_isBypass = value;
                }
            }
        }

        #region APP_UPDATE_MEMBER_VARS

        /// <summary>
        /// Delegate we supply to the WinSparkle DLL, which it will use to check with us if a
        /// shutdown is okay. We can reply yes or no to this request. If we reply yes, then
        /// WinSparkle will make an official shutdown request, allowing us to cleanly shutdown before
        /// getting updated.
        /// </summary>
        private WinSparkle.WinSparkleCanShutdownCheckCallback m_winsparkleShutdownCheckCb;

        /// <summary>
        /// Delegate we supply to the WinSparkle DLL, which if we've given permission, means that
        /// when WinSparkle invokes this method, we are to cleanly shutdown so that WinSparkle can
        /// complete an application update.
        /// </summary>
        private WinSparkle.WinSparkleRequestShutdownCallback m_winsparkleShutdownRequestCb;

        #endregion APP_UPDATE_MEMBER_VARS

        #region FilteringEngineVars

        /// <summary>
        /// Used to strip multiple whitespace.
        /// </summary>
        private Regex m_whitespaceRegex;

        /// <summary>
        /// Used for synchronization whenever our NLP model gets updated while we're already
        /// initialized.
        /// </summary>
        private ReaderWriterLockSlim m_doccatSlimLock = new ReaderWriterLockSlim();

        private DocumentCategorizerME m_documentClassifier;

        private Engine m_filteringEngine;

        private BackgroundWorker m_filterEngineStartupBgWorker;

        private Engine.FirewallCheckHandler m_firewallCheckCb;

        private Engine.ClassifyContentHandler m_classifyCb;
        

        /// <summary>
        /// Whenever we load filtering rules, we simply make up numbers for categories as we go
        /// along. We use this object to store what strings we map to numbers.
        /// </summary>
        private ConcurrentDictionary<string, FilterListEntry> m_generatedCategoriesMap = new ConcurrentDictionary<string, FilterListEntry>(StringComparer.OrdinalIgnoreCase);

        #endregion FilteringEngineVars

        /// <summary>
        /// Used for synchronization when creating run at startup task.
        /// </summary>
        private ReaderWriterLockSlim m_runAtStartupLock = new ReaderWriterLockSlim();

        /// <summary>
        /// Timer used to query for filter list changes every X minutes, as well as application
        /// updates.
        /// </summary>
        private Timer m_updateCheckTimer;

        /// <summary>
        /// Since clean shutdown can be called from a couple of different places, we'll use this and
        /// some locks to ensure it's only done once.
        /// </summary>
        private volatile bool m_cleanShutdownComplete = false;

        /// <summary>
        /// Used to ensure clean shutdown once.
        /// </summary>
        private Object m_cleanShutdownLock = new object();

        /// <summary>
        /// Holds a record of the result from the last time the user's authenticity was challenged.
        /// </summary>
        private bool m_lastAuthWasSuccess = false;

        /// <summary>
        /// Logger.
        /// </summary>
        private readonly Logger m_logger;

        /// <summary>
        /// Shown when the program is minimized to the tray. The app is always minimized to the tray
        /// on close.
        /// </summary>
        private System.Windows.Forms.NotifyIcon m_trayIcon;

        /// <summary>
        /// This BackgroundWorker object handles initializing the application off the UI thread.
        /// Allows the splash screen to function.
        /// </summary>
        private BackgroundWorker m_backgroundInitWorker;

        /// <summary>
        /// Primary and only window we use.
        /// </summary>
        private MainWindow m_mainWindow;

        /// <summary>
        /// App function config file.
        /// </summary>
        private AppConfigModel m_config;

        /// <summary>
        /// This int stores the number of block actions that have elapsed within the given threshold
        /// timespan.
        /// </summary>
        private long m_thresholdTicks;

        /// <summary>
        /// This timer resets the threshold tick count.
        /// </summary>
        private Timer m_thresholdCountTimer;

        /// <summary>
        /// This timer is used when the threshold has been hit. It is used to set an expiry period
        /// for the internet lockout once the threshold has been hit.
        /// </summary>
        private Timer m_thresholdEnforcementTimer;

        /// <summary>
        /// Stores all, if any, applications that should be forced throught the filter.
        /// </summary>
        private HashSet<string> m_blacklistedApplications = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Stores all, if any, applications that should not be forced through the filter.
        /// </summary>
        private HashSet<string> m_whitelistedApplications = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// This timer is used to count down to the expiry time for relaxed policy use.
        /// </summary>
        private Timer m_relaxedPolicyExpiryTimer;

        /// <summary>
        /// This timer is used to track a 24 hour cooldown period after the exhaustion of all
        /// available relaxed policy uses. Once the timer is expired, it will reset the count to the
        /// config default and then disable itself.
        /// </summary>
        private Timer m_relaxedPolicyResetTimer;

        #region Views

        /// <summary>
        /// This view is shown whenever a valid auth or re-auth request cannot be performed, or has
        /// never been performed.
        /// </summary>
        private LoginView m_viewLogin;

        /// <summary>
        /// This view is shown when auth with a certain provider is a success, and the conditions are
        /// to be laid out for user acceptance.
        /// </summary>
        private ProviderConditionsView m_viewProviderConditions;

        /// <summary>
        /// Used to show the user a nice spinny wheel while they wait for something.
        /// </summary>
        private ProgressWait m_viewProgressWait;

        /// <summary>
        /// Primary view for a subscribed, authenticated user.
        /// </summary>
        private DashboardView m_viewDashboard;

        #endregion Views

        /// <summary>
        /// Gets/sets whether or not this application should run at startup.
        /// </summary>
        public bool RunAtStartup
        {
            get
            {
                try
                {
                    m_runAtStartupLock.EnterReadLock();

                    Process p = new Process();
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.StartInfo.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    p.StartInfo.FileName = "schtasks";
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.Arguments += string.Format("/nh /fo TABLE /tn \"{0}\"", System.Diagnostics.Process.GetCurrentProcess().ProcessName);
                    p.StartInfo.RedirectStandardError = true;
                    p.Start();

                    string output = p.StandardOutput.ReadToEnd();
                    string errorOutput = p.StandardError.ReadToEnd();

                    p.WaitForExit();

                    var runAtStartup = false;
                    if(p.ExitCode == 0 && output.IndexOf(System.Diagnostics.Process.GetCurrentProcess().ProcessName) != -1)
                    {
                        runAtStartup = true;
                    }

                    return runAtStartup;
                }
                finally
                {
                    m_runAtStartupLock.ExitReadLock();
                }
            }

            set
            {
                try
                {
                    // You MUST call this before entering the write lock, otherwise you're gonna
                    // deadlock your app.
                    var currentValue = RunAtStartup;

                    m_runAtStartupLock.EnterWriteLock();

                    Process p = new Process();
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.StartInfo.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    p.StartInfo.FileName = "schtasks";
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.RedirectStandardError = true;

                    switch(value)
                    {
                        case true:
                            {
                                string createTaskCommand = string.Format("/create /F /sc onlogon /tn \"{0}\" /rl highest /tr \"'", System.Diagnostics.Process.GetCurrentProcess().ProcessName) + System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName + "'/StartMinimized\"";
                                p.StartInfo.Arguments += createTaskCommand;

                                // Only create an entry if there isn't already one.
                                if(currentValue == false)
                                {
                                    p.Start();
                                    p.WaitForExit();
                                }
                            }
                            break;

                        case false:
                            {
                                string deleteTaskCommand = string.Format("/delete /F /tn \"{0}\"", System.Diagnostics.Process.GetCurrentProcess().ProcessName);
                                p.StartInfo.Arguments += deleteTaskCommand;
                                p.Start();
                                p.WaitForExit();
                            }
                            break;
                    }
                }
                finally
                {
                    m_runAtStartupLock.ExitWriteLock();
                }
            }
        }

        /// <summary>
        /// Default ctor.
        /// </summary>
        public CitadelApp()
        {
            // Set a global to hold the base URI of the service providers address. This must be the
            // base path where the server side auth system is hosted.
            Application.Current.Properties["ServiceProviderApi"] = "https://manage.cloudveil.org/citadel";

            m_logger = LogManager.GetLogger("Citadel");

            this.Startup += CitadelOnStartup;
        }

        private void CitadelOnStartup(object sender, StartupEventArgs e)
        {
            // Hook the shutdown/logoff event.
            SystemEvents.SessionEnded += OnOsShutdownOrLogoff;

            // Hook app exiting function. This must be done on this main app thread.
            this.Exit += OnApplicationExiting;

            // Do stuff that must be done on the UI thread first.
            InitViews();

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // Run the background init worker for non-UI related initialization.
            m_backgroundInitWorker = new BackgroundWorker();
            m_backgroundInitWorker.DoWork += DoBackgroundInit;
            m_backgroundInitWorker.RunWorkerCompleted += OnBackgroundInitComplete;

            m_backgroundInitWorker.RunWorkerAsync(e);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception err = e.ExceptionObject as Exception;
            LoggerUtil.RecursivelyLogException(m_logger, err);
        }

        /// <summary>
        /// Called only in circumstances where the application config requires use of the block
        /// action threshold tracking functionality.
        /// </summary>
        private void InitThresholdData()
        {            
            // If exists, stop it first.
            if(m_thresholdCountTimer != null)
            {
                m_thresholdCountTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }

            // Create the threshold count timer and start it with the configured timespan.
            m_thresholdCountTimer = new Timer(OnThresholdTriggerPeriodElapsed, null, m_config.ThresholdTriggerPeriod, Timeout.InfiniteTimeSpan);

            // If exists, stop it first.
            if(m_thresholdEnforcementTimer != null)
            {
                m_thresholdEnforcementTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }

            // Create the enforcement timer, but don't start it.
            m_thresholdEnforcementTimer = new Timer(OnThresholdTimeoutPeriodElapsed, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Called to initialize the various application views on startup.
        /// </summary>
        private void InitViews()
        {
            m_mainWindow = new Citadel.UI.Windows.MainWindow();

            m_mainWindow.Closing += ((object sender, CancelEventArgs e) =>
            {
                if(!AuthenticatedUserModel.Instance.HasAcceptedTerms)
                {
                    // If terms have not been accepted, and window is closed, just full blown exit
                    // the app.
                    Current.Shutdown(ExitCodes.ShutdownWithoutSafeguards);
                    return;
                }

                // Don't actually let the window close, just hide it.
                e.Cancel = true;

                // When the main window closes, go to tray and show notification.
                MinimizeToTray(true);
            });

            m_viewLogin = new LoginView();

            if(m_viewLogin.DataContext != null && m_viewLogin.DataContext is BaseCitadelViewModel)
            {
                ((BaseCitadelViewModel)(m_viewLogin.DataContext)).ViewChangeRequest += OnViewChangeRequest;
            }

            m_viewProgressWait = new ProgressWait();

            m_viewProviderConditions = new ProviderConditionsView();

            if(m_viewProviderConditions.DataContext != null && m_viewProviderConditions.DataContext is BaseCitadelViewModel)
            {
                ((BaseCitadelViewModel)(m_viewProviderConditions.DataContext)).ViewChangeRequest += OnViewChangeRequest;
            }

            m_viewDashboard = new DashboardView();

            if(m_viewDashboard.DataContext != null && m_viewDashboard.DataContext is BaseCitadelViewModel)
            {
                ((BaseCitadelViewModel)(m_viewDashboard.DataContext)).ViewChangeRequest += OnViewChangeRequest;
            }

            // Set the current view to ProgressWait because we're gonna do background init next.
            m_mainWindow.Show();
            OnViewChangeRequest(typeof(ProgressWait));
        }

        /// <summary>
        /// Downloads, if necessary and able, a fresh copy of the filtering data for this user.
        /// </summary>
        /// <returns>
        /// True if new list data was downloaded, false otherwise.
        /// </returns>
        private bool UpdateListData()
        {
            var currentRemoteListsHashReq = WebServiceUtil.RequestResource("/capi/datacheck.php");
            currentRemoteListsHashReq.Wait();
            var rHashBytes = currentRemoteListsHashReq.Result;

            if(rHashBytes != null)
            {
                var rhash = Encoding.UTF8.GetString(rHashBytes);

                var listDataFilePath = AppDomain.CurrentDomain.BaseDirectory + "a.dat";

                bool needsUpdate = false;

                if(!File.Exists(listDataFilePath))
                {
                    needsUpdate = true;
                }
                else
                {
                    // We're going to hash our local version and compare. If they don't match, we're
                    // going to update our lists.

                    using(var fs = File.OpenRead(listDataFilePath))
                    {
                        using(SHA256 sec = new SHA256CryptoServiceProvider())
                        {
                            byte[] bt = sec.ComputeHash(fs);
                            var lHash = BitConverter.ToString(bt).Replace("-", "");

                            if(!lHash.OIEquals(rhash))
                            {
                                needsUpdate = true;
                            }
                        }
                    }
                }

                if(needsUpdate)
                {
                    m_logger.Info("Updating filtering rules, rules missing or integrity violation.");
                    var filterListDataReq = WebServiceUtil.RequestResource("/capi/getdata.php");
                    filterListDataReq.Wait();

                    var filterDataZipBytes = filterListDataReq.Result;

                    if(filterDataZipBytes != null)
                    {
                        File.WriteAllBytes(listDataFilePath, filterDataZipBytes);
                    }
                    else
                    {
                        Debug.WriteLine("Failed to download list data.");
                        m_logger.Error("Failed to download list data.");
                    }
                }

                return needsUpdate;
            }

            return false;
        }

        /// <summary>
        /// This will cause WinSparkle to begin checking for application updates.
        /// </summary>
        private void StartCheckForAppUpdates()
        {
            try
            {
                WinSparkle.CheckUpdateWithoutUI();
            }
            catch(Exception e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
        }

        /// <summary>
        /// Inits all the callbacks for WinSparkle, so that when we call for update checks and such,
        /// it has all appropriate callbacks to request app shutdown, restart, etc, to allow for
        /// updating.
        /// </summary>
        private void InitWinsparkle()
        {
            try
            {
                m_winsparkleShutdownCheckCb = new WinSparkle.WinSparkleCanShutdownCheckCallback(WinSparkleCheckIfShutdownOkay);
                m_winsparkleShutdownRequestCb = new WinSparkle.WinSparkleRequestShutdownCallback(WinSparkleRequestsShutdown);

                var appcastUrl = string.Empty;
                var baseServiceProviderAddress = (string)Application.Current.Properties["ServiceProviderApi"];
                if(Environment.Is64BitProcess)
                {
                    appcastUrl = baseServiceProviderAddress + "/update/winx64/update.xml";
                }
                else
                {
                    appcastUrl = baseServiceProviderAddress + "/update/winx86/update.xml";
                }

                m_logger.Info("Setting appcast to {0}.", appcastUrl);

                WinSparkle.SetCanShutdownCallback(m_winsparkleShutdownCheckCb);
                WinSparkle.SetShutdownRequestCallback(m_winsparkleShutdownRequestCb);
                WinSparkle.SetAppcastUrl(appcastUrl);
            }
            catch(Exception e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
        }

        /// <summary>
        /// Sets up the filtering engine, gets discovered installations of firefox to trust the
        /// engine, sets up callbacks for classification and firewall checks, but does not start the
        /// engine.
        /// </summary>
        private void InitEngine()
        {
            // Get our CA-Bundle resource and unpack it to the application directory.
            var caCertPackURI = new Uri("pack://application:,,,/Resources/ca-cert.pem");
            var resourceStream = GetResourceStream(caCertPackURI);
            TextReader tsr = new StreamReader(resourceStream.Stream);
            var caFileText = tsr.ReadToEnd();

            // Get our blocked HTML page
            var blockedPagePackURI = new Uri("pack://application:,,,/Resources/BlockedPage.html");
            resourceStream = GetResourceStream(blockedPagePackURI);
            tsr = new StreamReader(resourceStream.Stream);
            var blockedHtmlPage = tsr.ReadToEnd();

            // Dump the text to the local file system.
            var localCaBundleCertPath = AppDomain.CurrentDomain.BaseDirectory + "ca-cert.pem";
            File.WriteAllText(localCaBundleCertPath, caFileText);

            X509Certificate2Collection collection = new X509Certificate2Collection(new X509Certificate2(Encoding.UTF8.GetBytes(caFileText)));
            foreach(X509Certificate2 x509 in collection)
            {
                Debug.WriteLine("certificate name: {0}", x509.Subject);
            }

            // Set firewall CB.
            m_firewallCheckCb = OnAppFirewallCheck;

            // Set classification CB.
            m_classifyCb = OnClassifyContent;

            // Init the engine with our callbacks, the path to the ca-bundle, let it pick whatever
            // ports it wants for listening, and give it our total processor count on this machine as
            // a hint for how many threads to use.
            m_filteringEngine = new Engine(m_firewallCheckCb, m_classifyCb, localCaBundleCertPath, blockedHtmlPage, 0, 0, (uint)Environment.ProcessorCount);

            // Setup block event info callbacks.
            m_filteringEngine.OnElementsBlocked += OnElementsBlocked;
            m_filteringEngine.OnRequestBlocked += OnRequestBlocked;

            // Setup general info, warning and error events.
            m_filteringEngine.OnInfo += EngineOnInfo;
            m_filteringEngine.OnWarning += EngineOnWarning;
            m_filteringEngine.OnError += EngineOnError;

            // Now establish trust with FireFox. XXX TODO - This can actually be done elsewhere. We
            // used to have to do this after the engine started up to wait for it to write the CA to
            // disk and then use certutil to install it in FF. However, thanks to FireFox giving the
            // option to trust the local certificate store, we don't have to do that anymore.
            EstablishTrustWithFirefox();
        }

        /// <summary>
        /// Initializes the NLP classification with the given model and list of categories from
        /// within the model that we'll consider enabled. That is to say, any classification result
        /// that yeilds a category found in the supplied json list of categories will trigger a block
        /// action.
        /// </summary>
        /// <param name="nlpModelBytes">
        /// The bytes from a loaded NLP classification model.
        /// </param>
        /// <param name="jsonCategories">
        /// A JSON serialized array of category names within the supplied model that will be treated
        /// as enabled. Classification results that yeild a category listed in this array will
        /// trigger a block action.
        /// </param>
        /// <remarks>
        /// Note that this must be called AFTER we have already initialized the filtering engine,
        /// because we make calls to enable new categories.
        /// </remarks>
        private void InitNlp(byte[] nlpModelBytes, string jsonCategories)
        {
            try
            {
                var selectedCategoriesList = JsonConvert.DeserializeObject<List<string>>(jsonCategories);
                var selectedCategoriesHashset = new HashSet<string>(selectedCategoriesList, StringComparer.OrdinalIgnoreCase);

                // Init our regexes
                m_whitespaceRegex = new Regex(@"\s+", RegexOptions.ECMAScript | RegexOptions.Compiled);

                // Init Document classifier.
                var doccatModel = new DoccatModel(new java.io.ByteArrayInputStream(nlpModelBytes));
                m_documentClassifier = new DocumentCategorizerME(doccatModel);

                // Get the number of categories and iterate over all categories in the model.
                var numCategories = m_documentClassifier.getNumberOfCategories();

                for(int i = 0; i < numCategories; ++i)
                {
                    var modelCategory = m_documentClassifier.getCategory(i);

                    if(selectedCategoriesHashset.Contains(modelCategory))
                    {
                        // This is an enabled category. Make the category name unique by prepending
                        // NLP
                        modelCategory = "NLP" + modelCategory;

                        m_logger.Info("Setting up NLP classification category: {0}", modelCategory);

                        FilterListEntry existingCategory;
                        if(!m_generatedCategoriesMap.TryGetValue(modelCategory, out existingCategory))
                        {
                            // We can't generate anymore categories. Sorry, but the rest get ignored.
                            if(m_generatedCategoriesMap.Count >= byte.MaxValue)
                            {
                                break;
                            }

                            existingCategory = new FilterListEntry();
                            existingCategory.CategoryName = modelCategory;
                            existingCategory.IsBypass = false;
                            existingCategory.CategoryId = (byte)((m_generatedCategoriesMap.Count) + 1);

                            m_generatedCategoriesMap.GetOrAdd(modelCategory, existingCategory);
                        }

                        m_filteringEngine.SetCategoryEnabled(existingCategory.CategoryId, true);
                    }
                }

                m_doccatSlimLock.EnterWriteLock();
            }
            finally
            {
                m_doccatSlimLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Runs initialization off the UI thread.
        /// </summary>
        /// <param name="sender">
        /// Event origin.
        /// </param>
        /// <param name="e">
        /// Event args.
        /// </param>
        private void DoBackgroundInit(object sender, DoWorkEventArgs e)
        {
            // Setup our tray icon.
            InitTrayIcon();

            // Get WinSparkle ready to work.
            InitWinsparkle();

            var authTask = ChallengeUserAuthenticity();
            authTask.Wait();
            m_lastAuthWasSuccess = authTask.Result;

            // Init the Engine in the background.
            InitEngine();

            var startupArgs = e.Argument as StartupEventArgs;

            if(startupArgs != null)
            {
                bool startMinimized = false;
                for(int i = 0; i != startupArgs.Args.Length; ++i)
                {
                    if(startupArgs.Args[i].OIEquals("/StartMinimized"))
                    {
                        startMinimized = true;
                        break;
                    }
                }

                if(startMinimized)
                {
                    MinimizeToTray(false);
                }
            }
        }

        /// <summary>
        /// Called whenever the user is logging off or shutting down the system. Here we simply react
        /// to the event by safely terminating the program.
        /// </summary>
        /// <param name="sender">
        /// Event origin.
        /// </param>
        /// <param name="e">
        /// Event args.
        /// </param>
        private void OnOsShutdownOrLogoff(object sender, SessionEndedEventArgs e)
        {
            // Unhook first.
            SystemEvents.SessionEnded -= OnOsShutdownOrLogoff;
            this.Exit -= OnApplicationExiting;

            m_logger.Info("Sessions log off or OS shutdown detected.");
            DoCleanShutdown(true);
        }

        /// <summary>
        /// Called when the application is about to exit.
        /// </summary>
        /// <param name="sender">
        /// Event origin.
        /// </param>
        /// <param name="e">
        /// Event args.
        /// </param>
        private void OnApplicationExiting(object sender, ExitEventArgs e)
        {
            // Unhook first.
            SystemEvents.SessionEnded -= OnOsShutdownOrLogoff;
            this.Exit -= OnApplicationExiting;

            m_logger.Info("Application shutdown detected.");

            if(e.ApplicationExitCode <= (int)ExitCodes.ShutdownWithSafeguards)
            {
                // ShutdownWithSafeguards == 0, so anything less than or equal to this is considered
                // to be a situation where we must install safegaurds to ensure application survival
                // and persistence.
                DoCleanShutdown(true);
            }
            else
            {
                DoCleanShutdown(false);
            }
        }

        /// <summary>
        /// Called when the background initialization function has returned.
        /// </summary>
        /// <param name="sender">
        /// Event origin.
        /// </param>
        /// <param name="e">
        /// Event args.
        /// </param>
        private void OnBackgroundInitComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            // Must ensure we're not blocking internet now that we're running.
            WFPUtility.EnableInternet();

            if(e.Cancelled || e.Error != null)
            {
                m_logger.Error("Error during initialization.");
                if(e.Error != null && m_logger != null)
                {
                    m_logger.Error(e.Error.Message);
                    m_logger.Error(e.Error.StackTrace);
                }

                Current.Shutdown(-1);
                return;
            }

            Current.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Normal,
                (Action)delegate ()
                {
                    if(m_lastAuthWasSuccess)
                    {
                        if(AuthenticatedUserModel.Instance.HasAcceptedTerms)
                        {
                            // Just go to dashboard.
                            OnViewChangeRequest(typeof(DashboardView));

                            // Check for updates when we have an already authenticated user.
                            StartCheckForAppUpdates();
                        }
                        else
                        {
                            // User still has not accepted terms. Show them.
                            OnViewChangeRequest(typeof(ProviderConditionsView));
                        }
                    }
                    else
                    {
                        OnViewChangeRequest(typeof(LoginView));
                    }
                }
            );
        }

        /// <summary>
        /// Initializes the m_trayIcon member, loading the icon graphic and hooking appropriate
        /// handlers to respond to user iteraction requesting to bring the application back out of
        /// the tray.
        /// </summary>
        private void InitTrayIcon()
        {
            m_trayIcon = new System.Windows.Forms.NotifyIcon();

            var iconPackUri = new Uri("pack://application:,,,/Resources/appicon.ico");
            var resourceStream = GetResourceStream(iconPackUri);

            m_trayIcon.Icon = new System.Drawing.Icon(resourceStream.Stream);

            m_trayIcon.DoubleClick +=
                delegate (object sender, EventArgs args)
                {
                    BringAppToFocus();
                };
        }

        /// <summary>
        /// Brings the main application window into focus for the user and removes it from the tray
        /// if the application icon is in the tray.
        /// </summary>
        public void BringAppToFocus()
        {
            Current.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Normal,
                (Action)delegate ()
                {
                    if(this.m_mainWindow != null)
                    {
                        this.m_mainWindow.Show();
                        this.m_mainWindow.WindowState = WindowState.Normal;
                    }

                    if(m_trayIcon != null)
                    {
                        m_trayIcon.Visible = false;
                    }
                }
            );
        }

        /// <summary>
        /// Checks to see if we can authenticate with the service provider, or if we have a
        /// previously saved result of an authentication request.
        /// </summary>
        /// <returns>
        /// True if a connection was established with the service provider, or if we discovered a
        /// previously saved, validated authentication request. False otherwise.
        /// </returns>
        private async Task<bool> ChallengeUserAuthenticity()
        {
            // Check if we have a stored session, and if not try and reload one.
            if(!AuthenticatedUserModel.Instance.HasStoredSession)
            {
                if(!AuthenticatedUserModel.Instance.LoadFromSave())
                {
                    return false;
                }

                // If we loaded, check again.
                if(!AuthenticatedUserModel.Instance.HasStoredSession)
                {
                    return false;
                }
            }

            var authResult = await AuthenticatedUserModel.Instance.ReAuthenticate();

            // If we have a saved session, but we can't connect, we'll allow the user to proceed.
            return authResult == AuthenticatedUserModel.AuthenticationResult.Success || authResult == AuthenticatedUserModel.AuthenticationResult.ConnectionFailed;
        }

        /// <summary>
        /// Handles a request from UserControls within the application to show a requested primary
        /// view type.
        /// </summary>
        /// <param name="viewType">
        /// The type of view requested.
        /// </param>
        private void OnViewChangeRequest(Type viewType)
        {
            try
            {
                Current.Dispatcher.Invoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    (Action)delegate ()
                    {
                        UIElement current = (UserControl)m_mainWindow.CurrentView.Content;
                        UIElement newView = null;

                        switch(viewType.Name)
                        {
                            case nameof(LoginView):
                                {
                                    newView = m_viewLogin;
                                }
                                break;

                            case nameof(ProgressWait):
                                {
                                    newView = m_viewProgressWait;
                                }
                                break;

                            case nameof(ProviderConditionsView):
                                {
                                    newView = m_viewProviderConditions;
                                }
                                break;

                            case nameof(DashboardView):
                                {
                                    newView = m_viewDashboard;

                                    // If we've been sent to the dashboard, the view from which no
                                    // one can escape, then that means we're all good to go and start
                                    // filtering.
                                    m_filterEngineStartupBgWorker = new BackgroundWorker();
                                    m_filterEngineStartupBgWorker.DoWork += ((object sender, DoWorkEventArgs e) =>
                                    {
                                        if(m_filteringEngine != null && !m_filteringEngine.IsRunning)
                                        {
                                            StartFiltering();
                                        }

                                        // During testing, we'll allow the deactivate button to also
                                        // function as a request for updates.
#if CITADEL_DEBUG
                                        StartCheckForAppUpdates();
#endif
                                    });

                                    m_filterEngineStartupBgWorker.RunWorkerAsync();
                                }
                                break;
                        }

                        if(newView != null && current != newView)
                        {
                            m_mainWindow.CurrentView.Content = newView;
                        }
                    }
                );
            }
            catch(Exception err)
            {
                LoggerUtil.RecursivelyLogException(m_logger, err);
            }
        }

        /// <summary>
        /// Searches for FireFox installations and enables trust of the local certificate store.
        /// </summary>
        /// <remarks>
        /// If any profile is discovered that does not have the local CA cert store checking enabled
        /// already, all instances of firefox will be killed and then restarted when calling this
        /// method.
        /// </remarks>
        private void EstablishTrustWithFirefox()
        {
            // Get the default FireFox profiles path.
            string defaultFirefoxProfilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            defaultFirefoxProfilesPath += @"\Mozilla\Firefox\Profiles";

            // Figure out if firefox is running. If later it is and we kill it, store the path to
            // firefox.exe so we can restart the process after we're done.
            string firefoxExePath = string.Empty;
            bool firefoxIsRunning = Process.GetProcessesByName("firefox").Length > 0;

            var prefsFiles = Directory.GetFiles(defaultFirefoxProfilesPath, "prefs.js", SearchOption.AllDirectories);

            // Represents the root CA option in both the enabled and disabled states.
            var prefDisabled = "user_pref(\"security.enterprise_roots.enabled\", false);";
            var prefEnabled = "user_pref(\"security.enterprise_roots.enabled\", true);";

            var needsOverwriting = new List<string>();
            var needsAddition = new List<string>();

            foreach(var prefFile in prefsFiles)
            {
                var fileText = File.ReadAllText(prefFile);

                if(fileText.IndexOf(prefDisabled) != -1)
                {
                    // This profile has an entry explicitly disabling the root CA option.
                    needsOverwriting.Add(prefFile);
                }
                else if(fileText.IndexOf(prefEnabled) == -1)
                {
                    // This profile has no entry for the root CA option.
                    needsAddition.Add(prefFile);
                }
                else
                {
                    Debug.WriteLine(string.Format("FF Config {0} already propertly configured.", prefFile));
                }
            }

            if((needsOverwriting.Count + needsAddition.Count > 0))
            {
                if(firefoxIsRunning)
                {
                    // We need to kill firefox before editing the preferences, otherwise they'll just
                    // get overwritten.
                    foreach(var ff in Process.GetProcessesByName("firefox"))
                    {
                        firefoxExePath = ff.MainModule.FileName;

                        try
                        {
                            ff.Kill();
                            ff.Dispose();
                        }
                        catch { }
                    }
                }

                // Replace with the enabled param in files that have this option disabled.
                foreach(var prefFilePath in needsOverwriting)
                {
                    var fileText = File.ReadAllText(prefFilePath);
                    fileText = fileText.Replace(prefDisabled, prefEnabled);
                    File.WriteAllText(prefFilePath, fileText);
                }

                // Append the enabled param to files that don't have this option defined at all.
                foreach(var prefFilePath in needsAddition)
                {
                    var fileText = File.ReadAllText(prefFilePath);
                    fileText += Environment.NewLine + prefEnabled;
                    File.WriteAllText(prefFilePath, fileText);
                }
            }

            // Means we force closed at least once instance of firefox. Relaunch it now to cause it
            // to run restore.
            if(firefoxIsRunning && StringExtensions.Valid(firefoxExePath))
            {
                // Start the process and abandon our handle.
                using(var p = new Process())
                {
                    p.StartInfo.FileName = firefoxExePath;
                    p.StartInfo.UseShellExecute = false;
                    p.Start();
                }
            }
        }

        #region EngineCallbacks

        private void EngineOnInfo(string message)
        {
            m_logger.Info(message);
        }

        private void EngineOnWarning(string message)
        {
            m_logger.Warn(message);
        }

        private void EngineOnError(string message)
        {
            m_logger.Error(message);
        }

        /// <summary>
        /// Called back the engine whenever a request was blocked.
        /// </summary>
        /// <param name="category">
        /// The category of the rule that caused the block action.
        /// </param>
        /// <param name="payloadSizeBlocked">
        /// The total number of bytes in the response that was blocked before downloading.
        /// </param>
        /// <param name="fullRequest">
        /// The full request that was blocked.
        /// </param>
        private void OnRequestBlocked(byte category, uint payloadSizeBlocked, string fullRequest)
        {
            bool internetShutOff = false;

            if(m_config.UseThreshold)
            {
                var currentTicks = Interlocked.Increment(ref m_thresholdTicks);

                if(currentTicks >= m_config.ThresholdLimit)
                {
                    internetShutOff = true;

                    try
                    {
                        m_logger.Warn("Block action threshold met or exceeded. Disabling internet.");
                        WFPUtility.DisableInternet();                        
                    }
                    catch(Exception e)
                    {
                        LoggerUtil.RecursivelyLogException(m_logger, e);
                    }

                    this.m_thresholdEnforcementTimer.Change(m_config.ThresholdTimeoutPeriod, Timeout.InfiniteTimeSpan);
                }
            }

            // Add this blocked request to the dashboard.
            Current.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Normal,
                (Action)delegate ()
                {
                    if(m_viewDashboard != null)
                    {
                        m_viewDashboard.AppendBlockActionEvent(fullRequest);

                        if(internetShutOff)
                        {
                            var restoreDate = DateTime.Now.AddTicks(m_config.ThresholdTimeoutPeriod.Ticks);

                            m_viewDashboard.ShowDisabledInternetMessage(restoreDate);
                        }
                    }
                }
            );

            m_logger.Info(string.Format("Request Blocked: {0}", fullRequest));
        }

        private void OnElementsBlocked(uint numElementsRemoved, string fullRequest)
        {
            Debug.WriteLine("Elements blocked.");
        }

        private bool OnAppFirewallCheck(string appAbsolutePath)
        {
            if(m_blacklistedApplications.Count == 0 && m_whitelistedApplications.Count == 0)
            {
                // Just filter anything accessing port 80 and 443.
                return true;
            }

            var appName = Path.GetFileName(appAbsolutePath);

            if(m_blacklistedApplications.Count > 0 && m_blacklistedApplications.Contains(appName))
            {
                // Blacklist is in effect and this app is blacklisted. So, force it through.
                return true;
            }

            if(m_whitelistedApplications.Count > 0 && m_whitelistedApplications.Contains(appName))
            {
                // Whitelist is in effect and this app is whitelisted. So, don't force it through.
                return false;
            }

            return false;
        }

        private byte OnClassifyContent(byte[] data, string contentType)
        {
            try
            {
                m_doccatSlimLock.EnterReadLock();

                contentType = contentType.ToLower();

                //Debug.WriteLine("Content classification requested.");
                m_logger.Info(string.Format("Classify data of length {0} and type {1}.", data.Length, contentType));

                // Only attempt text classification if we have a text classifier, silly.
                if(m_documentClassifier != null)
                {
                    var textToClassifyBuilder = new StringBuilder();

                    if(contentType.IndexOf("html") != -1)
                    {
                        // This might be plain text, might be HTML. We need to find out.
                        var rawText = Encoding.UTF8.GetString(data);

                        HtmlDocument doc = new HtmlDocument();
                        doc.LoadHtml(rawText);

                        if(doc != null && doc.DocumentNode != null)
                        {
                            foreach(var script in doc.DocumentNode.Descendants("script").ToArray())
                            {
                                script.Remove();
                            }

                            foreach(var style in doc.DocumentNode.Descendants("style").ToArray())
                            {
                                style.Remove();
                            }

                            var allTextNodes = doc.DocumentNode.SelectNodes("//text()");
                            if(allTextNodes != null && allTextNodes.Count > 0)
                            {
                                foreach(HtmlNode node in allTextNodes)
                                {
                                    textToClassifyBuilder.Append(node.InnerText);
                                }
                            }

                            m_logger.Info("From HTML: Classify this string: {0}", m_whitespaceRegex.Replace(textToClassifyBuilder.ToString(), " "));
                        }
                    }
                    else if(contentType.IndexOf("json") != -1)
                    {
                        // This should be JSON.
                        var jsonText = Encoding.UTF8.GetString(data);

                        var len = jsonText.Length;
                        for(int i = 0; i < len; ++i)
                        {
                            char c = jsonText[i];
                            if(char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
                            {
                                textToClassifyBuilder.Append(c);
                            }
                            else
                            {
                                textToClassifyBuilder.Append(' ');
                            }
                        }

                        m_logger.Info("From Json: Classify this string: {0}", m_whitespaceRegex.Replace(textToClassifyBuilder.ToString(), " "));
                    }

                    var textToClassify = textToClassifyBuilder.ToString();

                    if(textToClassify.Length > 0)
                    {
                        m_logger.Info("Got text to classify of length {0}.", textToClassify.Length);

                        // Remove all multi-whitespace, newlines etc.
                        textToClassify = m_whitespaceRegex.Replace(textToClassify, " ");

                        var classificationResult = m_documentClassifier.categorize(textToClassify);

                        var bestCategoryName = "NLP" + m_documentClassifier.getBestCategory(classificationResult);

                        FilterListEntry categoryNumber = new FilterListEntry();

                        var bestCategoryScore = classificationResult.Max();

                        if(m_generatedCategoriesMap.TryGetValue(bestCategoryName, out categoryNumber))
                        {
                            if(categoryNumber.CategoryId > 0 && m_filteringEngine.IsCategoryEnabled(categoryNumber.CategoryId))
                            {
                                var threshold = .9;
                                if(bestCategoryScore < threshold)
                                {
                                    m_logger.Info("Rejected {0} classification because score was less than threshold of {1}. Returned score was {2}.", bestCategoryName, threshold, bestCategoryScore);
                                    return 0;
                                }

                                m_logger.Info("Classified text content as {0}.", bestCategoryName);
                                return categoryNumber.CategoryId;
                            }
                        }
                        else
                        {
                            m_logger.Info("Did not find category registered: {0}.", bestCategoryName);
                        }
                    }
                }
                else
                {
                    m_logger.Info("NLP classifier is null.");
                }
            }
            catch(Exception e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
            finally
            {
                m_doccatSlimLock.ExitReadLock();
            }

            // Default to zero. Means don't block this content.
            return 0;
        }

        #endregion EngineCallbacks

        /// <summary>
        /// Called by the threshold trigger timer whenever it's set time has passed. Here we'll reset
        /// the current count of block actions we're tracking.
        /// </summary>
        /// <param name="state">
        /// Async state object. Not used.
        /// </param>
        private void OnThresholdTriggerPeriodElapsed(object state)
        {
            this.m_thresholdCountTimer.Change(Timeout.Infinite, Timeout.Infinite);

            // Reset count to zero.
            Interlocked.Exchange(ref m_thresholdTicks, 0);

            this.m_thresholdCountTimer.Change(m_config.ThresholdTriggerPeriod, Timeout.InfiniteTimeSpan);
        }

        /// <summary>
        /// Called whenever the threshold timeout period has elapsed. Here we'll restore internet
        /// access.
        /// </summary>
        /// <param name="state">
        /// Async state object. Not used.
        /// </param>
        private void OnThresholdTimeoutPeriodElapsed(object state)
        {
            try
            {
                WFPUtility.EnableInternet();
            }
            catch(Exception e)
            {
                m_logger.Warn("Error when trying to reinstate internet on threshold timeout period elapsed.");
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }

            // Change UI state of dashboard to not show disabled message anymore.
            Current.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Normal,
                (Action)delegate ()
                {
                    if(m_viewDashboard != null)
                    {
                        m_viewDashboard.HideDisabledInternetMessage();
                    }
                }
            );

            // Disable the timer before we leave.
            this.m_thresholdEnforcementTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Called every X minutes by the update timer. We check for new lists, and hot-swap the
        /// rules if we have found new ones. We also check for program updates.
        /// </summary>
        /// <param name="state">
        /// This is always null. Ignore it.
        /// </param>
        private void OnUpdateTimerElapsed(object state)
        {
            m_logger.Info("Checking for filter list updates.");

            // Stop the threaded timer while we do this. We have to stop it in order to avoid
            // overlapping calls.
            this.m_updateCheckTimer.Change(Timeout.Infinite, Timeout.Infinite);

            // First, let's re-authenticate to ensure that we hold a current, valid session.
            var authResultTask = ChallengeUserAuthenticity();
            authResultTask.Wait();
            var authResult = authResultTask.Result;

            if(authResult)
            {
                bool gotUpdatedFilterLists = UpdateListData();

                if(gotUpdatedFilterLists)
                {
                    // Got new data. Gotta reload.
                    ReloadFilteringRules();
                }
            }
            else
            {
                m_logger.Warn("Failed re-authentication during list update process.");
            }

            m_logger.Info("Checking for application updates.");

            // Check for app updates.
            StartCheckForAppUpdates();

            // Enable the timer again.
            this.m_updateCheckTimer.Change(TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);
        }

        /// <summary>
        /// Starts the filtering engine.
        /// </summary>
        private void StartFiltering()
        {
            // Let's make sure we've pulled our internet block.
            try
            {
                WFPUtility.EnableInternet();
            }
            catch { }

            if(m_filteringEngine != null && !m_filteringEngine.IsRunning)
            {
                m_logger.Info("Start engine.");

                // Start the engine right away, to ensure the atomic bool IsRunning is set.
                m_filteringEngine.Start();

                // Make sure we have a task set to run again.
                RunAtStartup = true;

                // Make sure our lists are up to date.
                UpdateListData();

                ReloadFilteringRules();

                // Setup filter list update check timer.
                m_updateCheckTimer = new Timer(OnUpdateTimerElapsed, null, TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);
            }
        }

        /// <summary>
        /// Queries the service provider for updated filtering rules.
        /// </summary>
        private void ReloadFilteringRules()
        {
            if(m_filteringEngine == null)
            {
                m_logger.Error("Called ReloadFilteringRules without initializing engine.");
                return;
            }

            // Load our filtering list data.
            var listDataFilePath = AppDomain.CurrentDomain.BaseDirectory + "a.dat";

            if(File.Exists(listDataFilePath))
            {
                uint totalFiltersLoaded = 0;
                uint totalTriggersLoaded = 0;

                byte[] nlpModelBytes = null;
                string nlpCategoriesJson = string.Empty;

                using(var file = File.OpenRead(listDataFilePath))
                {
                    using(var zip = new ZipArchive(file, ZipArchiveMode.Read))
                    {
                        try
                        {
                            string cfgJson = string.Empty;
                            foreach(var entry in zip.Entries)
                            {
                                if(entry.Name.OIEquals("cfg.json"))
                                {
                                    using(var cfgStream = entry.Open())
                                    using(TextReader tr = new StreamReader(cfgStream))
                                    {
                                        cfgJson = tr.ReadToEnd();
                                        break;
                                    }
                                }
                            }

                            if(!StringExtensions.Valid(cfgJson))
                            {
                                m_logger.Error("Could not find valid JSON config for filter.");
                                return;
                            }

                            Debug.WriteLine(cfgJson);

                            // Deserialize config
                            try
                            {
                                m_config = JsonConvert.DeserializeObject<AppConfigModel>(cfgJson);
                            }
                            catch(Exception deserializationError)
                            {
                                m_logger.Error("Failed to deserialize JSON config.");
                                LoggerUtil.RecursivelyLogException(m_logger, deserializationError);
                                return;
                            }

                            // Setup blacklist or whitelisted apps.
                            foreach(var appName in m_config.BlacklistedApplications)
                            {
                                if(StringExtensions.Valid(appName))
                                {
                                    m_blacklistedApplications.Add(appName);
                                }
                            }

                            foreach(var appName in m_config.WhitelistedApplications)
                            {
                                if(StringExtensions.Valid(appName))
                                {
                                    m_whitelistedApplications.Add(appName);
                                }
                            }

                            if(m_config.UseThreshold)
                            {
                                // Setup the threshold timers and related data members.
                                InitThresholdData();
                            }

                            if(m_config.CannotTerminate)
                            {
                                // Turn on process protection if requested.
                                if(!ProcessProtection.IsProtected)
                                {
                                    ProcessProtection.Protect();
                                }
                            }

                            if(m_config.Bypass.Count > 0 && m_config.BypassesPermitted > 0)
                            {
                                Current.Dispatcher.BeginInvoke(
                                    System.Windows.Threading.DispatcherPriority.Normal,
                                    (Action)delegate ()
                                    {
                                        // Bypass lists have been enabled.
                                        if(m_viewDashboard.DataContext != null && m_viewDashboard.DataContext is DashboardViewModel)
                                        {
                                            var dashboardViewModel = ((DashboardViewModel)m_viewDashboard.DataContext);
                                            dashboardViewModel.AvailableRelaxedRequests = m_config.BypassesPermitted;
                                            dashboardViewModel.RelaxedDuration = new DateTime(m_config.BypassDuration.Ticks).ToString("HH:mm");

                                            dashboardViewModel.Model.RelaxedPolicyRequested += OnRelaxedPolicyRequested;

                                            dashboardViewModel.Model.RelinquishRelaxedPolicyRequested += OnRelinquishRelaxedPolicyRequested;
                                        }
                                    }
                                );
                            }

                            foreach(var entry in zip.Entries)
                            {
                                if(entry.Name.OIEquals("cfg.json"))
                                {
                                    continue;
                                }

                                var categoryName = entry.FullName;
                                var listName = entry.Name.ToLower();
                                var ssLen = categoryName.Length - (listName.Length + 1);

                                if(ssLen <= 0)
                                {
                                    // Just in case this is some glitch or issue where a top level file
                                    // has been included that is not part of any category. Skip such
                                    // an entry.
                                    continue;
                                }

                                categoryName = categoryName.Substring(0, ssLen);
                                
                                // Handle NLP entries, if any.
                                if(categoryName.OIEquals("nlp"))
                                {
                                    if(listName.OIEquals("categories.json"))
                                    {
                                        // This is a list of all categories within the model that
                                        // have been chosen for use.
                                        using(TextReader catReader = new StreamReader(entry.Open()))
                                        {
                                            nlpCategoriesJson = catReader.ReadToEnd();
                                            continue;
                                        }
                                    }
                                    else if(Path.GetExtension(listName).OIEquals(".model"))
                                    {
                                        // This is the NLP model itself.
                                        using(var modelFileStream = entry.Open())
                                        using(var modelMemStream = new MemoryStream())
                                        {
                                            modelFileStream.CopyTo(modelMemStream);
                                            nlpModelBytes = modelMemStream.ToArray();
                                            continue;
                                        }
                                    }
                                }

                                // Try and fetch an existing category matching this name, or create a new one.
                                FilterListEntry existingCategory;
                                if(!m_generatedCategoriesMap.TryGetValue(categoryName, out existingCategory))
                                {
                                    // We can't generate anymore categories. Sorry, but the rest get ignored.
                                    if(m_generatedCategoriesMap.Count >= byte.MaxValue)
                                    {
                                        break;
                                    }

                                    existingCategory = new FilterListEntry();
                                    existingCategory.CategoryName = categoryName;
                                    existingCategory.IsBypass = false;
                                    existingCategory.CategoryId = (byte)((m_generatedCategoriesMap.Count) + 1);

                                    // In case we're re-loading, call to unload any existing rules for this category first.
                                    m_filteringEngine.UnloadAllFilterRulesForCategory(existingCategory.CategoryId);

                                    m_generatedCategoriesMap.GetOrAdd(categoryName, existingCategory);
                                }

                                // Enable this category we've fetched here.
                                m_filteringEngine.SetCategoryEnabled(existingCategory.CategoryId, true);

                                uint rulesLoaded, rulesFailed;

                                switch(listName)
                                {
                                    case "rules.txt":                                    
                                        {
                                            // Need to prepend @@ to lines if it's a whitelist.
                                            string rulePrefix = m_config.Whitelists.Contains(entry.FullName) ? "@@" : string.Empty;
                                            bool isBypass = m_config.Bypass.Contains(entry.FullName);

                                            var builder = new StringBuilder();
                                            string line = null;
                                            using(TextReader tr = new StreamReader(entry.Open()))
                                            {   
                                                while((line = tr.ReadLine()) != null)
                                                {   
                                                    builder.Append(rulePrefix + line + "\n");
                                                }

                                                tr.Close();
                                                
                                                var listContents = builder.ToString();
                                                builder.Clear();

                                                if(StringExtensions.Valid(listContents))
                                                {
                                                    m_filteringEngine.LoadAbpFormattedString(listContents, existingCategory.CategoryId, false, out rulesLoaded, out rulesFailed);
                                                    totalTriggersLoaded += rulesLoaded;
                                                    listContents = string.Empty;
                                                }

                                                // Set if it's a bypass list or not.
                                                existingCategory.IsBypass = isBypass;

                                                // Force GC to run because this will clear A LOT of memory.
                                                System.GC.Collect();
                                            }
                                        }
                                        break;

                                    case "triggers.txt":
                                        {   
                                            using(TextReader tr = new StreamReader(entry.Open()))
                                            {
                                                var triggers = tr.ReadToEnd();
                                                if(StringExtensions.Valid(triggers))
                                                {
                                                    m_filteringEngine.LoadTextTriggersFromString(triggers, existingCategory.CategoryId, false, out rulesLoaded);
                                                    totalTriggersLoaded += rulesLoaded;
                                                    tr.Close();
                                                }                                               

                                                // Force GC to run because this will clear A LOT of memory.
                                                System.GC.Collect();
                                            }
                                        }
                                        break;
                                }
                            }
                        }
                        finally
                        {
                            zip.Dispose();
                            file.Close();
                            file.Dispose();
                        }
                    }
                }

                if(nlpModelBytes != null && StringExtensions.Valid(nlpCategoriesJson))
                {
                    m_logger.Info("Initializing NLP classification.");
                    InitNlp(nlpModelBytes, nlpCategoriesJson);
                }

                var listLoadInfo = string.Format("Loaded {0} filtering rules and {1} text triggers.", totalFiltersLoaded, totalTriggersLoaded);
                Debug.WriteLine(listLoadInfo);
                m_logger.Info(listLoadInfo);
            }
            else
            {
                m_logger.Error("No filtering rules.");
            }
        }

        /// <summary>
        /// Called whenever a relaxed policy has been requested.
        /// </summary>
        private void OnRelaxedPolicyRequested()
        {
            // Start the count down timer.
            if(m_relaxedPolicyExpiryTimer == null)
            {
                m_relaxedPolicyExpiryTimer = new Timer(OnRelaxedPolicyTimerExpired, null, Timeout.Infinite, Timeout.Infinite);
            }

            // Disable every category that is a bypass category.
            foreach(var entry in m_generatedCategoriesMap.Values)
            {
                if(entry.IsBypass)
                {
                    m_filteringEngine.SetCategoryEnabled(entry.CategoryId, false);
                }
            }

            m_relaxedPolicyExpiryTimer.Change(m_config.BypassDuration, Timeout.InfiniteTimeSpan);

            DecrementRelaxedPolicy();
        }

        private void DecrementRelaxedPolicy()
        {
            bool allUsesExhausted = false;

            Current.Dispatcher.Invoke(
                System.Windows.Threading.DispatcherPriority.Normal,
                (Action)delegate ()
                {
                    if(m_viewDashboard != null)
                    {
                        if(m_viewDashboard.DataContext != null && m_viewDashboard.DataContext is DashboardViewModel)
                        {
                            var dashboardViewModel = ((DashboardViewModel)m_viewDashboard.DataContext);

                            dashboardViewModel.AvailableRelaxedRequests -= 1;

                            if(dashboardViewModel.AvailableRelaxedRequests <= 0)
                            {
                                dashboardViewModel.AvailableRelaxedRequests = 0;
                                allUsesExhausted = true;
                            }
                        }
                    }
                }
            );

            if(allUsesExhausted)
            {
                // Refresh tomorrow at midnight.
                var today = DateTime.Now;
                var tomorrow = today.AddDays(1);
                var span = tomorrow - today;

                if(m_relaxedPolicyResetTimer == null)
                {
                    m_relaxedPolicyResetTimer = new Timer(OnRelaxedPolicyResetExpired, null, span, Timeout.InfiniteTimeSpan);
                }                

                m_relaxedPolicyResetTimer.Change(span, Timeout.InfiniteTimeSpan);
            }
        }

        /// <summary>
        /// Called when the user has manually requested to relinquish a relaxed policy.
        /// </summary>
        private void OnRelinquishRelaxedPolicyRequested()
        {
            bool relaxedInEffect = false;
            // Determine if a relaxed policy is currently in effect.
            foreach(var entry in m_generatedCategoriesMap.Values)
            {
                if(entry.IsBypass && m_filteringEngine.IsCategoryEnabled(entry.CategoryId) == false)
                {
                    relaxedInEffect = true;
                    break;
                }
            }
            
            // Ensure timer is stopped and re-enable categories by simply
            // calling the timer's expiry callback.
            OnRelaxedPolicyTimerExpired(null);

            // If a policy was not already in effect, then the user is choosing to
            // relinquish a policy not yet used. So just eat it up. If  this is not
            // the case, then the policy has already been decremented, so don't
            // bother.
            if(!relaxedInEffect)
            {
                DecrementRelaxedPolicy();
            }
        }

        /// <summary>
        /// Called whenever the relaxed policy duration has expired.
        /// </summary>
        /// <param name="state">
        /// Async state. Not used.
        /// </param>
        private void OnRelaxedPolicyTimerExpired(object state)
        {
            // Disable the expiry timer.
            m_relaxedPolicyExpiryTimer.Change(Timeout.Infinite, Timeout.Infinite);

            // Enable every category that is a bypass category.
            foreach(var entry in m_generatedCategoriesMap.Values)
            {
                if(entry.IsBypass)
                {
                    m_filteringEngine.SetCategoryEnabled(entry.CategoryId, true);
                }
            }           
        }

        /// <summary>
        /// Called whenever the relaxed policy reset timer has expired. This expiry refreshes the
        /// available relaxed policy requests to the configured value.
        /// </summary>
        /// <param name="state">
        /// Async state. Not used.
        /// </param>
        private void OnRelaxedPolicyResetExpired(object state)
        {
            // Disable the reset timer.
            m_relaxedPolicyResetTimer.Change(Timeout.Infinite, Timeout.Infinite);

            // Reset the available count.
            Current.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Normal,
                (Action)delegate ()
                {
                    if(m_viewDashboard != null)
                    {
                        if(m_viewDashboard.DataContext != null && m_viewDashboard.DataContext is DashboardViewModel)
                        {
                            var dashboardViewModel = ((DashboardViewModel)m_viewDashboard.DataContext);

                            dashboardViewModel.AvailableRelaxedRequests = m_config.BypassesPermitted;
                        }
                    }
                }
            );
        }

        /// <summary>
        /// Stops the filtering engine, shuts it down.
        /// </summary>
        private void StopFiltering()
        {
            if(m_filteringEngine != null && m_filteringEngine.IsRunning)
            {
                m_filteringEngine.Stop();
            }
        }

        /// <summary>
        /// Sends the application to the task tray, optionally showing a tooltip explaining that the
        /// application is now hiding away.
        /// </summary>
        /// <param name="showTip">
        /// Bool that determines if a short tooltip explaining that the application is now in the
        /// background.
        /// </param>
        private void MinimizeToTray(bool showTip = false)
        {
            Current.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Normal,
                (Action)delegate ()
                {
                    if(m_mainWindow != null && m_trayIcon != null)
                    {
                        m_trayIcon.Visible = true;
                        m_mainWindow.Visibility = Visibility.Hidden;

                        if(showTip)
                        {
                            m_trayIcon.ShowBalloonTip(1500, "Still Running", string.Format("{0} will continue running in the background.", System.Diagnostics.Process.GetCurrentProcess().ProcessName), System.Windows.Forms.ToolTipIcon.Info);
                        }
                    }
                }
            );
        }

        /// <summary>
        /// Called by WinSparkle when it wants to check if it is alright to shut down this
        /// application in order to install an update.
        /// </summary>
        /// <returns>
        /// Return zero if a shutdown is not okay at this time, return one if it is okay to shut down
        /// the application immediately after this function returns.
        /// </returns>
        private int WinSparkleCheckIfShutdownOkay()
        {
            // Winsparkle can always shut down. When we shut down, we disable the internet anyway,
            // and WinSparkle should have already downloaded the installer by the time it asks for a
            // shutdown. So, we're good to shut down any time.
            return 1;
        }

        /// <summary>
        /// Called by WinSparkle when it has confirmed that a shutdown is okay and WinSparkle is
        /// ready to shut this application down so it can install a downloaded update.
        /// </summary>
        private void WinSparkleRequestsShutdown()
        {
            Current.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Normal,
                (Action)delegate ()
                {
                    Application.Current.Shutdown(ExitCodes.ShutdownWithSafeguards);
                }
            );
        }

        /// <summary>
        /// Called whenever the app is shut down with an authorized key, or when the system is
        /// shutting down, or when the user is logging off.
        /// </summary>
        /// <param name="installSafeguards">
        /// Indicates whether or not safeguards should be put in place when we exit the application
        /// here. Safeguards means that we're going to do all that we can to ensure that our function
        /// is not bypassed, and that we're going to be forced to run again.
        /// </param>
        private void DoCleanShutdown(bool installSafeguards)
        {
            lock(m_cleanShutdownLock)
            {
                if(!m_cleanShutdownComplete)
                {
                    // Pull our icon from the task tray.
                    if(m_trayIcon != null)
                    {
                        m_trayIcon.Visible = false;
                    }

                    // Pull our critical status.
                    if(ProcessProtection.IsProtected)
                    {
                        ProcessProtection.Unprotect();
                    }

                    // Shut down engine.
                    StopFiltering();

                    if(installSafeguards)
                    {
                        // Ensure that we're run at startup, disable the internet, etc.
                        RunAtStartup = true;

                        if(m_config.BlockInternet)
                        {
                            // While we're here, let's disable the internet so that the user can't browse
                            // the web without us. Only do this of course if configured.
                            try
                            {
                                WFPUtility.DisableInternet();
                            }
                            catch { }
                        }
                    }
                    else
                    {
                        // Means that our user got a granted deactivation request, or installed but
                        // never activated.
                        m_logger.Info("Shutting down without safeguards.");
                    }

                    // Make sure our credentials are saved.
                    AuthenticatedUserModel.Instance.Save();

                    // Flag that clean shutdown was completed already.
                    m_cleanShutdownComplete = true;
                }
            }
        }
    }
}