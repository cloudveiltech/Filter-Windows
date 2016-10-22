using Microsoft.Win32;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Te.Citadel.Extensions;
using Te.Citadel.UI.Models;
using Te.Citadel.UI.ViewModels;
using Te.Citadel.UI.Views;
using Te.Citadel.UI.Windows;
using Te.Citadel.Util;
using Te.HttpFilteringEngine;
using opennlp.tools.doccat;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace Te.Citadel
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class CitadelApp : Application
    {
        #region FilteringEngineVars

        /// <summary>
        /// Used to strip multiple whitespace.
        /// </summary>
        private Regex m_whitespaceRegex;

        /// <summary>
        /// Used to strip chars from a string that are not A-Z0-9 or space.
        /// </summary>
        private Regex m_nonAlphaNumRegex;

        /// <summary>
        /// Used for synchronization whenever our NLP model gets updated while we're
        /// already initialized.
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
        private ConcurrentDictionary<string, byte> m_generatedCategoriesMap = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        #endregion

        /// <summary>
        /// Used for synchronization when creating run at startup task.
        /// </summary>
        private ReaderWriterLockSlim m_runAtStartupLock = new ReaderWriterLockSlim();

        /// <summary>
        /// Timer used to query for filter list changes every X minutes.
        /// </summary>
        private Timer m_updateFilterListsTimer;

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
                    p.StartInfo.Arguments += "/nh /fo TABLE /tn \"Citadel\"";
                    p.StartInfo.RedirectStandardError = true;
                    p.Start();

                    string output = p.StandardOutput.ReadToEnd();
                    string errorOutput = p.StandardError.ReadToEnd();

                    p.WaitForExit();

                    var runAtStartup = false;
                    if(p.ExitCode == 0 && output.IndexOf("Citadel") != -1)
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
                    // You MUST call this before entering the write lock, otherwise
                    // you're gonna deadlock your app.
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
                                string createTaskCommand = "/create /F /sc onlogon /tn \"Citadel\" /rl highest /tr \"'" + System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName + "'/StartMinimized\"";
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
                                string deleteTaskCommand = "/delete /F /tn \"Citadel\"";
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
            // Set a global to hold the base URI of the service providers address.
            // This must be the base path where the server side auth system is
            // hosted.
            Application.Current.Properties["ServiceProviderApi"] = "http://localhost/";

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
            InitAppWideCommands();
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
            Debug.WriteLine(err.Message);
        }

        /// <summary>
        /// Initializes commands made available to certain object types application-wide.
        /// </summary>
        private void InitAppWideCommands()
        {
            Debug.WriteLine("Running");

            // All commands in our app resources are designed for use by BaseCitadelViewModel
            // instances.
            foreach(CommandBinding commandBinding in Resources.Values.OfType<CommandBinding>())
            {
                // Give all view models in our application the ability to request view changes.
                CommandManager.RegisterClassCommandBinding(typeof(UserControl), commandBinding);
            }
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
                    // If terms have not been accepted, and window is closed, just full blown exit the app.
                    Application.Current.Shutdown();
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
                    // We're going to hash our local version and compare. If they
                    // don't match, we're going to update our lists.

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
                        // This is an enabled category.
                        // Make the category name unique by prepending NLP
                        modelCategory = "NLP" + modelCategory;

                        m_logger.Info("Setting up NLP classification category: {0}", modelCategory);

                        byte existingCategory = 0;
                        if(!m_generatedCategoriesMap.TryGetValue(modelCategory, out existingCategory))
                        {
                            // We can't generate anymore categories. Sorry, but the rest get ignored.
                            if(m_generatedCategoriesMap.Count >= byte.MaxValue)
                            {
                                break;
                            }

                            existingCategory = (byte)((m_generatedCategoriesMap.Count) + 1);

                            m_generatedCategoriesMap.GetOrAdd(modelCategory, existingCategory);
                        }

                        m_filteringEngine.SetCategoryEnabled(existingCategory, true);
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
            this.Exit -= OnApplicationExiting;

            DoCleanShutdown(false);
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

            // Check for updates, always.
            //WinSparkle.CheckUpdateWithoutUI();
        }

        /// <summary>
        /// Initializes the m_trayIcon member, loading the icon graphic and hooking appropriate
        /// handlers to respond to user iteraction requesting to bring the application back out of
        /// the tray.
        /// </summary>
        private void InitTrayIcon()
        {
            m_trayIcon = new System.Windows.Forms.NotifyIcon();

            var iconPackUri = new Uri("pack://application:,,,/Resources/citadel.ico");
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

            // If we have a saved session, but we can't connect, we'll allow the user 
            // to proceed.
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

                                    // If we've been sent to the dashboard, the view from which no one can escape,
                                    // then that means we're all good to go and start filtering.
                                    m_filterEngineStartupBgWorker = new BackgroundWorker();
                                    m_filterEngineStartupBgWorker.DoWork += ((object sender, DoWorkEventArgs e) =>
                                    {   
                                        StartFiltering();
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
                Debug.WriteLine(err.Message);
                Debug.WriteLine(err.StackTrace);
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
                    // We need to kill firefox before editing the preferences, otherwise they'll just get
                    // overwritten.                
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

        private void OnRequestBlocked(byte category, uint payloadSizeBlocked, string fullRequest)
        {
            m_logger.Info(string.Format("Request Blocked: {0}", fullRequest));
        }

        private void OnElementsBlocked(uint numElementsRemoved, string fullRequest)
        {
            Debug.WriteLine("Elements blocked.");
        }

        private bool OnAppFirewallCheck(string appAbsolutePath)
        {
            Debug.WriteLine(string.Format("Filtering app {0}.", appAbsolutePath));
            // Just filter anything accessing port 80 and 443.
            return true;
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

                        foreach(var script in doc.DocumentNode.Descendants("script").ToArray())
                        {
                            script.Remove();
                        }

                        foreach(var style in doc.DocumentNode.Descendants("style").ToArray())
                        {
                            style.Remove();
                        }   

                        foreach(HtmlNode node in doc.DocumentNode.SelectNodes("//text()"))
                        {
                            textToClassifyBuilder.Append(node.InnerText);                            
                        }

                        m_logger.Info("From HTML: Classify this string: {0}", m_whitespaceRegex.Replace(textToClassifyBuilder.ToString(), " "));
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

                        
                       //char[] arr = jsonText.ToCharArray();
                       //
                       //arr = Array.FindAll<char>(arr, (c => (char.IsLetterOrDigit(c)
                       //                                  || char.IsWhiteSpace(c)
                       //                                  || c == '-')));
                       //
                       //textToClassifyBuilder = new StringBuilder(new string(arr));
                        

                        m_logger.Info("From Json: Classify this string: {0}", m_whitespaceRegex.Replace(textToClassifyBuilder.ToString(), " "));

                        
                       //JObject obj = JObject.Parse(jsonText);
                       //foreach(var pair in obj)
                       //{
                       //    textToClassifyBuilder.Append(pair.Key).Append(" ").Append(pair.Value).Append(" ");
                       //}
                        


                    }
                    



                    var textToClassify = textToClassifyBuilder.ToString();

                    if(textToClassify.Length > 0)
                    {
                        m_logger.Info("Got text to classify of length {0}.", textToClassify.Length);

                        // Remove all multi-whitespace, newlines etc.
                        textToClassify = m_whitespaceRegex.Replace(textToClassify, " ");

                        var classificationResult = m_documentClassifier.categorize(textToClassify);

                        var bestCategoryName = "NLP" + m_documentClassifier.getBestCategory(classificationResult);

                        byte categoryNumber = 0;

                        var bestCategoryScore = classificationResult.Max();

                        if(m_generatedCategoriesMap.TryGetValue(bestCategoryName, out categoryNumber))
                        {
                            if(categoryNumber > 0 && m_filteringEngine.IsCategoryEnabled(categoryNumber))
                            {
                                var threshold = .9;
                                if(bestCategoryScore < threshold)
                                {
                                    m_logger.Info("Rejected {0} classification because score was less than threshold of {1}. Returned score was {2}.", bestCategoryName, threshold, bestCategoryScore);
                                    return 0;
                                }

                                m_logger.Info("Classified text content as {0}.", bestCategoryName);
                                return categoryNumber;
                            }
                        }
                    }
                }

                // Default to zero. Means don't block this content.
                return 0;
            }
            finally
            {
                m_doccatSlimLock.ExitReadLock();
            }
        }

        #endregion

        /// <summary>
        /// Called every X minutes by the filter list update timer. We check for new lists, and
        /// hot-swap the rules if we have found new ones.
        /// </summary>
        /// <param name="state">
        /// This is always null. Ignore it.
        /// </param>
        private void OnFilterListUpdateTimer(object state)
        {
            m_logger.Info("Checking for filter list updates.");

            // Stop the threaded timer while we do this. We have to stop it in order
            // to avoid overlapping calls.
            this.m_updateFilterListsTimer.Change(Timeout.Infinite, Timeout.Infinite);

            bool gotUpdatedFilterLists = UpdateListData();

            if(gotUpdatedFilterLists)
            {
                // Got new data. Gotta reload.
                ReloadFilteringRules();
            }

            // Enable the timer again.
            this.m_updateFilterListsTimer.Change(TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);
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
            
            // Make sure we have a task set to run again.
            RunAtStartup = true;
            
            // Make sure our lists are up to date.
            UpdateListData();
            
            ReloadFilteringRules();            

            if(m_filteringEngine != null && !m_filteringEngine.IsRunning)
            {   
                m_filteringEngine.Start();
            }
            else
            {   
                m_logger.Info("Can't start engine.");
            }
            
            // Setup filter list update check timer.
            m_updateFilterListsTimer = new Timer(OnFilterListUpdateTimer, null, TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);            
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
                        foreach(var entry in zip.Entries)
                        {
                            // Extract the category name and text file from this filter data entry.
                            var categoryName = entry.FullName.ToLower();
                            var listName = entry.Name.ToLower();
                            categoryName = categoryName.Substring(0, categoryName.Length - (listName.Length + 1));

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
                            byte existingCategory = 0;
                            if(!m_generatedCategoriesMap.TryGetValue(categoryName, out existingCategory))
                            {
                                // We can't generate anymore categories. Sorry, but the rest get ignored.
                                if(m_generatedCategoriesMap.Count >= byte.MaxValue)
                                {
                                    break;
                                }

                                existingCategory = (byte)((m_generatedCategoriesMap.Count) + 1);

                                // In case we're re-loading, call to unload any existing rules for this category first.
                                m_filteringEngine.UnloadAllFilterRulesForCategory(existingCategory);

                                m_generatedCategoriesMap.GetOrAdd(categoryName, existingCategory);
                            }

                            // Just turn this category on.
                            m_logger.Info("Enabling category: " + existingCategory);
                            m_filteringEngine.SetCategoryEnabled(existingCategory, true);

                            bool areTriggers = false;

                            switch(listName)
                            {
                                case "rules.txt":
                                case "triggers.txt":
                                    {
                                        // If it's a whitelist category, we need to prepend whitelist markers to each line.
                                        string prefix = categoryName.OIEquals("whitelist") ? "@@" : string.Empty;

                                        var builder = new StringBuilder();
                                        var c = 0;
                                        using(TextReader tr = new StreamReader(entry.Open()))
                                        {
                                            string line = null;
                                            while((line = tr.ReadLine()) != null)
                                            {
                                                ++c;
                                                builder.Append(prefix + line + "\n");
                                            }
                                            
                                            tr.Close();
                                        }

                                        areTriggers = listName.OIEquals("triggers.txt");

                                        uint rulesLoaded, rulesFailed;
                                        if(areTriggers)
                                        {   
                                            var listContents = builder.ToString();
                                            builder.Clear();
                                            m_filteringEngine.LoadTextTriggersFromString(listContents, existingCategory, false, out rulesLoaded);
                                            totalTriggersLoaded += rulesLoaded;
                                            listContents = string.Empty;

                                            // Force GC to run because this will clear A LOT of memory.
                                            System.GC.Collect();
                                        }
                                        else
                                        {
                                            var listContents = builder.ToString();
                                            builder.Clear();
                                            m_filteringEngine.LoadAbpFormattedString(listContents, existingCategory, false, out rulesLoaded, out rulesFailed);
                                            totalFiltersLoaded += rulesLoaded;
                                            listContents = string.Empty;
                                            listContents = null;

                                            // Force GC to run because this will clear A LOT of memory.
                                            System.GC.Collect();
                                        }
                                    }
                                    break;
                            }
                        }

                        zip.Dispose();
                    }

                    file.Close();
                    file.Dispose();
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
                            m_trayIcon.ShowBalloonTip(1500, "Still Running", "Citadel will continue running in the background.", System.Windows.Forms.ToolTipIcon.Info);
                        }
                    }
                }
            );
        }

        /// <summary>
        /// Called whenever the app is shut down with an authorized key, or when the system is
        /// shutting down, or when the user is logging off.
        /// </summary>
        /// <param name="isDueToShutdownOrLogoff">
        /// Indicates whether or not this is being called because of a logoff/shutdown event, or
        /// if it's just being called because the application is exiting. If it's the latter, then
        /// we can assume that the user has gotten permission to deactivate, so we don't want
        /// to disable the internet or anything like that.
        /// </param>
        private void DoCleanShutdown(bool isDueToShutdownOrLogoff)
        {
            lock(m_cleanShutdownLock)
            {
                if(!m_cleanShutdownComplete)
                {
                    // Pull our critical status.
                    if(ProcessProtection.IsProtected)
                    {
                        ProcessProtection.Unprotect();
                    }

                    // Shut down engine.
                    StopFiltering();

                    if(AuthenticatedUserModel.Instance.HasAcceptedTerms)
                    {
                        // Ensure that we're always run at startup, even in safe mode.
                        // But, only do this if the user has accepted the terms of the app.
                        // And also only do this if this shutdown isn't from the user
                        // getting a granted deactivation request.

                        if(isDueToShutdownOrLogoff)
                        {
                            m_logger.Info("Shutting down due to logoff or OS shutdown.");
                            RunAtStartup = true;

                            // While we're here, let's disable the internet so that the user
                            // can't browse the web without us.                            
                            try
                            {
                                WFPUtility.DisableInternet();
                            }
                            catch { }
                        }
                        else
                        {
                            // Means that our user got a granted deactivation request.
                            m_logger.Info("Shutting down due to granted deactivation request.");
                        }
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