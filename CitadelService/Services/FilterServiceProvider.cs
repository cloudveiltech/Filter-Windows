/*
* Copyright © 2017 Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Citadel.Core.Extensions;
using Citadel.Core.WinAPI;
using Citadel.Core.Windows.Util;
using Citadel.Core.Windows.Util.Update;
using Citadel.IPC;
using Citadel.IPC.Messages;
using CitadelService.Data.Filtering;
using CitadelService.Data.Models;
using DistillNET;
using Microsoft.Win32;
using Newtonsoft.Json;
using NLog;
using opennlp.tools.doccat;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Te.Citadel.Util;
using Te.HttpFilteringEngine;
using WindowsFirewallHelper;

namespace CitadelService.Services
{
    internal class FilterServiceProvider
    {
        #region Windows Service API

        public bool Start()
        {
            try
            {
                OnStartup();
            }
            catch(Exception e)
            {
                // Critical failure.
                return false;
            }

            return true;
        }

        public bool Stop()
        {
            // We always return false because we don't let anyone tell us that we're going to stop.
            return false;
        }

        public bool Shutdown()
        {
            // Called on a shutdown event.
            Environment.Exit((int)ExitCodes.ShutdownWithSafeguards);
            return true;
        }

        #endregion Windows Service API

        private FilterStatus Status
        {
            get
            {
                try
                {
                    m_currentStatusLock.EnterReadLock();

                    return m_currentStatus;
                }
                finally
                {
                    m_currentStatusLock.ExitReadLock();
                }
            }

            set
            {
                try
                {
                    m_currentStatusLock.EnterWriteLock();

                    if(m_authRequiredFlag && value != FilterStatus.AwaitingCredentials)
                    {
                        // Don't let our status change at all if we're waiting for auth. This is critical.
                        m_currentStatus = FilterStatus.AwaitingCredentials;
                    }
                    else
                    {
                        m_currentStatus = value;
                    }

                    if(value == FilterStatus.AwaitingCredentials)
                    {
                        m_authRequiredFlag = true;
                    }
                }
                finally
                {
                    m_currentStatusLock.ExitWriteLock();
                }

                m_ipcServer.NotifyStatus(value);
            }
        }

        private int ConnectedClients
        {
            get
            {
                return Interlocked.CompareExchange(ref m_connectedClients, m_connectedClients, 0);
            }

            set
            {
                Interlocked.Exchange(ref m_connectedClients, value);
            }
        }

        private volatile bool m_authRequiredFlag = false;

        /// <summary>
        /// Our current filter status. 
        /// </summary>
        private FilterStatus m_currentStatus = FilterStatus.Synchronizing;

        /// <summary>
        /// Our status lock. 
        /// </summary>
        private ReaderWriterLockSlim m_currentStatusLock = new ReaderWriterLockSlim();

        /// <summary>
        /// The number of IPC clients connected to this server. 
        /// </summary>
        private int m_connectedClients = 0;
        
        #region FilteringEngineVars

        /// <summary>
        /// Used to strip multiple whitespace. 
        /// </summary>
        private Regex m_whitespaceRegex;

        private CategoryIndex m_categoryIndex = new CategoryIndex(short.MaxValue);

        private IPCServer m_ipcServer;

        /// <summary>
        /// Used for synchronization whenever our NLP model gets updated while we're already initialized. 
        /// </summary>
        private ReaderWriterLockSlim m_doccatSlimLock = new ReaderWriterLockSlim();

        private List<CategoryMappedDocumentCategorizerModel> m_documentClassifiers = new List<CategoryMappedDocumentCategorizerModel>();

        private Engine m_filteringEngine;

        private BackgroundWorker m_filterEngineStartupBgWorker;

        private Engine.FirewallCheckHandler m_firewallCheckCb;

        private Engine.HttpMessageBeginHandler m_httpMessageBeginCb;

        private Engine.HttpMessageEndHandler m_httpMessageEndCb;

        private string m_blockedHtmlPage;

        private static readonly DateTime s_Epoch = new DateTime(1970, 1, 1);

        private static readonly string s_EpochHttpDateTime = s_Epoch.ToString("r");

        /// <summary>
        /// Applications we never ever want to filter. Right now, this is just OS binaries. 
        /// </summary>
        private static readonly HashSet<string> s_foreverWhitelistedApplications = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Whenever we load filtering rules, we simply make up numbers for categories as we go
        /// along. We use this object to store what strings we map to numbers.
        /// </summary>
        private ConcurrentDictionary<string, MappedFilterListCategoryModel> m_generatedCategoriesMap = new ConcurrentDictionary<string, MappedFilterListCategoryModel>(StringComparer.OrdinalIgnoreCase);

        #endregion FilteringEngineVars

        private ReaderWriterLockSlim m_filteringRwLock = new ReaderWriterLockSlim();

        private ReaderWriterLockSlim m_updateRwLock = new ReaderWriterLockSlim();

        private FilterDbCollection m_filterCollection;

        private BagOfTextTriggers m_textTriggers;

        /// <summary>
        /// Used for synchronization when creating run at startup task. 
        /// </summary>
        private ReaderWriterLockSlim m_runAtStartupLock = new ReaderWriterLockSlim();

        /// <summary>
        /// Timer used to query for filter list changes every X minutes, as well as application updates. 
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
        /// Logger. 
        /// </summary>
        private readonly Logger m_logger;

        /// <summary>
        /// This BackgroundWorker object handles initializing the application off the UI thread.
        /// Allows the splash screen to function.
        /// </summary>
        private BackgroundWorker m_backgroundInitWorker;

        /// <summary>
        /// App function config file. 
        /// </summary>
        private AppConfigModel m_config;

        /// <summary>
        /// Json deserialization/serialization settings for our config related data. 
        /// </summary>
        private JsonSerializerSettings m_configSerializerSettings;

        /// <summary>
        /// This int stores the number of block actions that have elapsed within the given threshold timespan.
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

        /// <summary>
        /// This timer is used to monitor local NIC cards and enforce DNS settings when they are
        /// configured in the application config.
        /// </summary>
        private Timer m_dnsEnforcementTimer;

        /// <summary>
        /// Used to ensure synchronized access when setting DNS settings. 
        /// </summary>
        private object m_dnsEnforcementLock = new object();

        private AppcastUpdater m_updater = null;

        private ApplicationUpdate m_lastFetchedUpdate = null;

        private ReaderWriterLockSlim m_appcastUpdaterLock = new ReaderWriterLockSlim();

        /// <summary>
        /// Default ctor. 
        /// </summary>
        public FilterServiceProvider()
        {
            m_logger = LoggerUtil.GetAppWideLogger();

            // Enforce good/proper protocols
            ServicePointManager.SecurityProtocol = (ServicePointManager.SecurityProtocol & ~SecurityProtocolType.Ssl3) | (SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12);
        }

        private void OnStartup()
        {
            // Hook the shutdown/logoff event.
            SystemEvents.SessionEnding += OnAppSessionEnding;

            // Hook app exiting function. This must be done on this main app thread.
            AppDomain.CurrentDomain.ProcessExit += OnApplicationExiting;

            try
            {
                var bitVersionUri = string.Empty;
                if(Environment.Is64BitProcess)
                {
                    bitVersionUri = "/update/winx64/update.xml";
                }
                else
                {
                    bitVersionUri = "/update/winx86/update.xml";
                }

                var appUpdateInfoUrl = string.Format("{0}/{1}", WebServiceUtil.Default.ServiceProviderApiPath, bitVersionUri);

                m_logger.Info("Checking for application updates at {0}.", appUpdateInfoUrl);

                m_updater = new AppcastUpdater(new Uri(appUpdateInfoUrl));
            }
            catch(Exception e)
            {
                // This is a critical error. We cannot recover from this.
                m_logger.Error("Critical error - Could not create application updater.");
                LoggerUtil.RecursivelyLogException(m_logger, e);

                Environment.Exit(-1);
            }

            try
            {
                m_ipcServer = new IPCServer();

                m_ipcServer.AttemptAuthentication = (args) =>
                {
                    ChallengeUserAuthenticity(args.Username, args.Password);
                };

                m_ipcServer.ClientAcceptedPendingUpdate = () =>
                {
                    try
                    {
                        m_appcastUpdaterLock.EnterWriteLock();

                        if(m_lastFetchedUpdate != null)
                        {
                            m_lastFetchedUpdate.DownloadUpdate().Wait();

                            m_ipcServer.NotifyUpdating();
                            m_lastFetchedUpdate.BeginInstallUpdateDelayed();                            
                            Task.Delay(200).Wait();

                            m_logger.Info("Shutting down to update.");

                            if(m_appcastUpdaterLock.IsWriteLockHeld)
                            {
                                m_appcastUpdaterLock.ExitWriteLock();
                            }

                            Environment.Exit((int)ExitCodes.ShutdownForUpdate);
                        }
                    }
                    catch(Exception e)
                    {
                        LoggerUtil.RecursivelyLogException(m_logger, e);
                    }
                    finally
                    {
                        if(m_appcastUpdaterLock.IsWriteLockHeld)
                        {
                            m_appcastUpdaterLock.ExitWriteLock();
                        }
                    }
                    
                };

                m_ipcServer.DeactivationRequested = (args) =>
                {
                    Status = FilterStatus.Synchronizing;

                    try
                    {
                        var response = WebServiceUtil.Default.RequestResource(ServiceResource.DeactivationRequest).Result;

                        args.Granted = response != null;

                        if(args.Granted)
                        {
                            Environment.Exit((int)ExitCodes.ShutdownWithoutSafeguards);
                        }
                        else
                        {
                            Status = FilterStatus.Running;
                        }
                    }
                    catch(Exception e)
                    {
                        LoggerUtil.RecursivelyLogException(m_logger, e);
                        Status = FilterStatus.Running;
                    }
                };

                m_ipcServer.ClientServerStateQueried = (args) =>
                {
                    m_ipcServer.NotifyStatus(Status);
                };

                m_ipcServer.RelaxedPolicyRequested = (args) =>
                {
                    switch(args.Command)
                    {
                        case RelaxedPolicyCommand.Relinquished:
                        {
                            OnRelinquishRelaxedPolicyRequested();
                        }
                        break;

                        case RelaxedPolicyCommand.Requested:
                        {
                            OnRelaxedPolicyRequested();
                        }
                        break;
                    }
                };

                m_ipcServer.ClientConnected = () =>
                {
                    ConnectedClients++;

                    // When a client connects, synchronize our data. Presently, we just want to
                    // update them with relaxed policy NFO, if any.
                    if(m_config != null && m_config.BypassesPermitted > 0)
                    {
                        m_ipcServer.NotifyRelaxedPolicyChange(m_config.BypassesPermitted - m_config.BypassesUsed, m_config.BypassDuration);
                    }
                    else
                    {
                        m_ipcServer.NotifyRelaxedPolicyChange(0, TimeSpan.Zero);
                    }

                    m_ipcServer.NotifyStatus(Status);
                };

                m_ipcServer.ClientDisconnected = () =>
                {
                    ConnectedClients--;
                };
            }
            catch(Exception ipce)
            {
                // This is a critical error. We cannot recover from this.
                m_logger.Error("Critical error - Could not start IPC server.");
                LoggerUtil.RecursivelyLogException(m_logger, ipce);                

                Environment.Exit(-1);
            }

            // Before we do any network stuff, ensure we have windows firewall access.
            EnsureWindowsFirewallAccess();

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // Run the background init worker for non-UI related initialization.
            m_backgroundInitWorker = new BackgroundWorker();
            m_backgroundInitWorker.DoWork += DoBackgroundInit;
            m_backgroundInitWorker.RunWorkerCompleted += OnBackgroundInitComplete;

            m_backgroundInitWorker.RunWorkerAsync();
        }

        private void EnsureWindowsFirewallAccess()
        {
            try
            {
                string thisProcessName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
                var thisAssembly = System.Reflection.Assembly.GetExecutingAssembly();

                // Get all existing rules matching our process name and destroy them.
                var myRules = FirewallManager.Instance.Rules.Where(r => r.Name.Equals(thisProcessName, StringComparison.OrdinalIgnoreCase)).ToArray();
                if(myRules != null && myRules.Length > 0)
                {
                    foreach(var rule in myRules)
                    {
                        FirewallManager.Instance.Rules.Remove(rule);
                    }
                }

                // Create inbound/outbound firewall rules and add them.
                var inboundRule = FirewallManager.Instance.CreateApplicationRule(
                    FirewallProfiles.Domain | FirewallProfiles.Private | FirewallProfiles.Public,
                    thisProcessName,
                    FirewallAction.Allow, thisAssembly.Location
                );
                inboundRule.Direction = FirewallDirection.Inbound;

                FirewallManager.Instance.Rules.Add(inboundRule);

                var outboundRule = FirewallManager.Instance.CreateApplicationRule(
                    FirewallProfiles.Domain | FirewallProfiles.Private | FirewallProfiles.Public,
                    thisProcessName,
                    FirewallAction.Allow, thisAssembly.Location
                );
                outboundRule.Direction = FirewallDirection.Outbound;

                FirewallManager.Instance.Rules.Add(outboundRule);
            }
            catch(Exception e)
            {
                m_logger.Error("Error while attempting to configure firewall application exception.");
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
        }

        private void OnAppSessionEnding(object sender, SessionEndingEventArgs e)
        {
            m_logger.Info("Session ending.");

            // THIS MUST BE DONE HERE ALWAYS, otherwise, we get BSOD.
            ProcessProtection.Unprotect();
            
            Environment.Exit((int)ExitCodes.ShutdownWithSafeguards);
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


            // Create the enforcement timer, but don't start it.
            m_thresholdEnforcementTimer = new Timer(OnThresholdTimeoutPeriodElapsed, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Downloads, if necessary and able, a fresh copy of the filtering data for this user. 
        /// </summary>
        /// <returns>
        /// True if new list data was downloaded, false otherwise. 
        /// </returns>
        private bool UpdateListData()
        {
            var currentRemoteListsHashReq = WebServiceUtil.Default.RequestResource(ServiceResource.UserDataSumCheck);
            currentRemoteListsHashReq.Wait();
            var rHashBytes = currentRemoteListsHashReq.Result;

            if(rHashBytes != null)
            {
                var rhash = Encoding.UTF8.GetString(rHashBytes);

                var listDataFilePath = AppDomain.CurrentDomain.BaseDirectory + "a.dat";

                bool needsUpdate = false;

                if(!File.Exists(listDataFilePath) || new FileInfo(listDataFilePath).Length == 0)
                {
                    needsUpdate = true;
                }
                else
                {
                    // We're going to hash our local version and compare. If they don't match, we're
                    // going to update our lists.

                    using(var fs = File.OpenRead(listDataFilePath))
                    {
                        using(SHA1 sec = new SHA1CryptoServiceProvider())
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
                    var filterListDataReq = WebServiceUtil.Default.RequestResource(ServiceResource.UserDataRequest);
                    filterListDataReq.Wait();

                    var filterDataZipBytes = filterListDataReq.Result;

                    if(filterDataZipBytes != null && filterDataZipBytes.Length > 0)
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

        private void ProbeMasterForApplicationUpdates()
        {   
            Status = FilterStatus.Synchronizing;

            try
            {
                m_appcastUpdaterLock.EnterWriteLock();

                m_lastFetchedUpdate = m_updater.CheckForUpdate().Result;

                if(m_lastFetchedUpdate != null)
                {
                    m_logger.Info("Found update. Asking clients to accept update.");
                    m_ipcServer.NotifyApplicationUpdateAvailable(new ServerUpdateQueryMessage(m_lastFetchedUpdate.Title, m_lastFetchedUpdate.HtmlBody, m_lastFetchedUpdate.CurrentVersion.ToString(), m_lastFetchedUpdate.UpdateVersion.ToString()));
                }
            }
            catch(Exception e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
            finally
            {
                m_appcastUpdaterLock.ExitWriteLock();

                Status = FilterStatus.Running;
            }
        }

        /// <summary>
        /// Sets up the filtering engine, gets discovered installations of firefox to trust the
        /// engine, sets up callbacks for classification and firewall checks, but does not start the engine.
        /// </summary>
        private void InitEngine()
        {
            // Get our CA-Bundle resource and unpack it to the application directory.
            var caCertPackURI = "CitadelService.Resources.ca-cert.pem";
            StringBuilder caFileBuilder = new StringBuilder();
            using(var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(caCertPackURI))
            {
                if(resourceStream != null && resourceStream.CanRead)
                {
                    using(TextReader tsr = new StreamReader(resourceStream))
                    {
                        caFileBuilder = new StringBuilder(tsr.ReadToEnd());
                    }
                }
                else
                {
                    m_logger.Error("Cannot read from packed ca bundle resource.");
                }
            }

            caFileBuilder.AppendLine();

            // Get our blocked HTML page
            var blockedPagePackURI = "CitadelService.Resources.BlockedPage.html";
            using(var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(blockedPagePackURI))
            {
                if(resourceStream != null && resourceStream.CanRead)
                {
                    using(TextReader tsr = new StreamReader(resourceStream))
                    {
                        m_blockedHtmlPage = tsr.ReadToEnd();
                    }
                }
                else
                {
                    m_logger.Error("Cannot read from packed block page file.");
                }
            }

            m_filterCollection = new FilterDbCollection(AppDomain.CurrentDomain.BaseDirectory + "rules.db", true, true);

            m_textTriggers = new BagOfTextTriggers(AppDomain.CurrentDomain.BaseDirectory + "t.dat", true, true);

            // Get Microsoft root authorities. We need this in order to permit Windows Update and
            // such in the event that it is forced through the filter.
            var toTrust = new List<StoreName>() {
                StoreName.Root,
                StoreName.AuthRoot,
                StoreName.CertificateAuthority,
                StoreName.TrustedPublisher,
                StoreName.TrustedPeople
            };

            foreach(var trust in toTrust)
            {
                using(X509Store localStore = new X509Store(trust, StoreLocation.LocalMachine))
                {
                    localStore.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                    foreach(var cert in localStore.Certificates)
                    {
                        caFileBuilder.AppendLine(cert.ExportToPem());

                        /* XXX TODO - Instead of only trusting microsoft CA's, let's trust all machine-local CA's.
                         * It's not unreasonable to expect the user and OS to handle these properly.
                        if(cert.Subject.IndexOf("Microsoft") != -1 && cert.Subject.IndexOf("Root") != -1)
                        {
                            m_logger.Info("Adding cert: {0}.", cert.Subject);
                            caFileBuilder.AppendLine(cert.ExportToPem());
                        }
                        */
                    }
                    localStore.Close();
                }
            }

            // Dump the text to the local file system.
            var localCaBundleCertPath = AppDomain.CurrentDomain.BaseDirectory + "ca-cert.pem";
            File.WriteAllText(localCaBundleCertPath, caFileBuilder.ToString());

            // Set firewall CB.
            m_firewallCheckCb = OnAppFirewallCheck;

            m_httpMessageBeginCb = OnHttpMessageBegin;

            m_httpMessageEndCb = OnHttpMessageEnd;

            // Init the engine with our callbacks, the path to the ca-bundle, let it pick whatever
            // ports it wants for listening, and give it our total processor count on this machine as
            // a hint for how many threads to use.
            m_filteringEngine = new Engine(m_firewallCheckCb, m_httpMessageBeginCb, m_httpMessageEndCb, localCaBundleCertPath, 0, 0, (uint)Environment.ProcessorCount);

            // Setup general info, warning and error events.
            m_filteringEngine.OnInfo += EngineOnInfo;
            m_filteringEngine.OnWarning += EngineOnWarning;
            m_filteringEngine.OnError += EngineOnError;

            // Now establish trust with FireFox. XXX TODO - This can actually be done elsewhere. We
            // used to have to do this after the engine started up to wait for it to write the CA to
            // disk and then use certutil to install it in FF. However, thanks to FireFox giving the
            // option to trust the local certificate store, we don't have to do that anymore.
            try
            {
                EstablishTrustWithFirefox();
            }
            catch(Exception ffe)
            {
                LoggerUtil.RecursivelyLogException(m_logger, ffe);
            }
        }

        /// <summary>
        /// Loads the given NLP model and list of categories from within the model that we'll
        /// consider enabled. That is to say, any classification result that yeilds a category found
        /// in the supplied list of enabled categories found within the loaded model will trigger a
        /// block action.
        /// </summary>
        /// <param name="nlpModelBytes">
        /// The bytes from a loaded NLP classification model. 
        /// </param>
        /// <param name="nlpConfig">
        /// A model file describing data about the model, such as a list of categories that, should
        /// they be returned by the classifer, should trigger a block action.
        /// </param>
        /// <remarks>
        /// Note that this must be called AFTER we have already initialized the filtering engine,
        /// because we make calls to enable new categories within the engine.
        /// </remarks>
        private void LoadNlpModel(byte[] nlpModelBytes, NLPConfigurationModel nlpConfig)
        {
            try
            {
                m_doccatSlimLock.EnterWriteLock();

                var selectedCategoriesHashset = new HashSet<string>(nlpConfig.SelectedCategoryNames, StringComparer.OrdinalIgnoreCase);

                var mappedAllCategorySet = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                // Init our regexes
                m_whitespaceRegex = new Regex(@"\s+", RegexOptions.ECMAScript | RegexOptions.Compiled);

                // Init Document classifier.
                var doccatModel = new DoccatModel(new java.io.ByteArrayInputStream(nlpModelBytes));
                var classifier = new DocumentCategorizerME(doccatModel);

                // Get the number of categories and iterate over all categories in the model.
                var numCategories = classifier.getNumberOfCategories();

                for(int i = 0; i < numCategories; ++i)
                {
                    var modelCategory = classifier.getCategory(i);

                    // Make the category name unique by prepending the relative path the NLP model
                    // file. This will ensure that categories with the same name across multiple NLP
                    // models will be insulated against collision.
                    var relativeNlpPath = nlpConfig.RelativeModelPath.Substring(0, nlpConfig.RelativeModelPath.LastIndexOfAny(new[] { '/', '\\' }) + 1) + Path.GetFileNameWithoutExtension(nlpConfig.RelativeModelPath) + "/";
                    var mappedModelCategory = relativeNlpPath + modelCategory;

                    mappedAllCategorySet.Add(modelCategory, mappedModelCategory);

                    if(selectedCategoriesHashset.Contains(modelCategory))
                    {
                        m_logger.Info("Setting up NLP classification category: {0}", modelCategory);

                        MappedFilterListCategoryModel existingCategory = null;
                        if(TryFetchOrCreateCategoryMap(mappedModelCategory, out existingCategory))
                        {
                            m_categoryIndex.SetIsCategoryEnabled(existingCategory.CategoryId, true);
                        }
                        else
                        {
                            m_logger.Error("Failed to get category map for NLP model.");
                        }
                    }
                }

                // Push this classifier to our list of classifiers.
                m_documentClassifiers.Add(new CategoryMappedDocumentCategorizerModel(classifier, mappedAllCategorySet));
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
            // Setup json serialization settings.
            m_configSerializerSettings = new JsonSerializerSettings();
            m_configSerializerSettings.NullValueHandling = NullValueHandling.Ignore;

            // Init the Engine in the background.
            try
            {
                InitEngine();
            }
            catch(Exception ie)
            {
                LoggerUtil.RecursivelyLogException(m_logger, ie);
            }

            // Force start our cascade of protective processes.
            try
            {
                ServiceSpawner.Instance.InitializeServices();
            }
            catch(Exception se)
            {
                LoggerUtil.RecursivelyLogException(m_logger, se);
            }

            // Init update timer.
            m_updateCheckTimer = new Timer(OnUpdateTimerElapsed, null, TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);

            ChallengeUserAuthenticity();
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
        private void OnApplicationExiting(object sender, EventArgs e)
        {
            m_logger.Info("Filter service provider process exiting.");

            try
            {
                // Unhook first.
                AppDomain.CurrentDomain.ProcessExit -= OnApplicationExiting;
            }
            catch(Exception err)
            {
                LoggerUtil.RecursivelyLogException(m_logger, err);
            }
            try
            {
                if(Environment.ExitCode == (int)ExitCodes.ShutdownWithoutSafeguards)
                {
                    m_logger.Info("Filter service provider process shutting down without safeguards.");

                    DoCleanShutdown(false);
                }
                else
                {
                    m_logger.Info("Filter service provider process shutting down with safeguards.");

                    // Unless explicitly told not to, always use safeguards.
                    DoCleanShutdown(true);
                }
            }
            catch(Exception err)
            {
                LoggerUtil.RecursivelyLogException(m_logger, err);
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
                    LoggerUtil.RecursivelyLogException(m_logger, e.Error);
                }

                Environment.Exit(-1);
                return;
            }

            if(WebServiceUtil.Default.HasStoredCredentials)
            {   
                ProbeMasterForApplicationUpdates();
            }
            else
            {
                m_ipcServer.NotifyAuthenticationStatus(Citadel.IPC.Messages.AuthenticationAction.Required);
            }
        }

        /// <summary>
        /// Checks to see if we can authenticate with the service provider, or if we have a
        /// previously saved result of an authentication request.
        /// </summary>
        /// <returns>
        /// True if a connection was established with the service provider, or if we discovered a
        /// previously saved, validated authentication request. False otherwise.
        /// </returns>
        private void ChallengeUserAuthenticity(string userName = null, SecureString password = null)
        {
            try
            {
                if(!string.IsNullOrEmpty(userName) && !string.IsNullOrWhiteSpace(userName) && password != null && password.Length > 0)
                {
                    byte[] unencrypedPwordBytes = null;
                    try
                    {
                        unencrypedPwordBytes = password.SecureStringBytes();

                        var res = WebServiceUtil.Default.Authenticate(userName, unencrypedPwordBytes);

                        PostAuthenticationResultToClients(res);
                    }
                    finally
                    {
                        if(unencrypedPwordBytes != null && unencrypedPwordBytes.Length > 0)
                        {
                            Array.Clear(unencrypedPwordBytes, 0, unencrypedPwordBytes.Length);
                            unencrypedPwordBytes = null;
                        }
                    }
                }

                if(!WebServiceUtil.Default.IsSessionExpired)
                {
                    PostAuthenticationResultToClients(AuthenticationResult.Success);
                    return;
                }

                // Check if we have a stored session, and if not try and reload one.
                if(!WebServiceUtil.Default.HasStoredCredentials)
                {
                    if(!WebServiceUtil.Default.LoadFromSave())
                    {
                        m_logger.Info("Failed to load saved instance of athenticated user.");
                        m_ipcServer.NotifyAuthenticationStatus(AuthenticationAction.Required);
                        return;
                    }

                    // If we loaded, check again.
                    if(!WebServiceUtil.Default.HasStoredCredentials)
                    {
                        m_logger.Info("Authenticated does not have stored credentials. Redirecting user to login.");
                        m_ipcServer.NotifyAuthenticationStatus(AuthenticationAction.Required);
                        return;
                    }
                }

                var authTaskResult = WebServiceUtil.Default.ReAuthenticate();

                PostAuthenticationResultToClients(authTaskResult);
            }
            catch(Exception e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
        }

        private void PostAuthenticationResultToClients(AuthenticationResult authResult)
        {
            // As long as we didn't explicitly fail authentication or reauthentication, ensure that
            // the filter runs/is running.
            if(authResult != AuthenticationResult.Failure)
            {
                if(m_filteringEngine != null && !m_filteringEngine.IsRunning)
                {
                    m_filterEngineStartupBgWorker = new BackgroundWorker();
                    m_filterEngineStartupBgWorker.DoWork += ((object sender, DoWorkEventArgs e) =>
                    {
                        StartFiltering();
                    });

                    m_filterEngineStartupBgWorker.RunWorkerAsync();
                }
            }

            switch(authResult)
            {
                case AuthenticationResult.Success:
                {
                    Status = FilterStatus.Running;
                    m_authRequiredFlag = false;
                    m_ipcServer.NotifyAuthenticationStatus(AuthenticationAction.Authenticated);
                }
                break;

                case AuthenticationResult.Failure:
                {
                    Status = FilterStatus.AwaitingCredentials;
                    m_authRequiredFlag = true;
                    m_ipcServer.NotifyAuthenticationStatus(AuthenticationAction.Required);

                    // Auth failed for this user. Shut down the filter.
                    StopFiltering();
                }
                break;

                case AuthenticationResult.ConnectionFailed:
                {
                    m_ipcServer.NotifyAuthenticationStatus(AuthenticationAction.ErrorNoInternet);
                }
                break;
            }
        }

        /// <summary>
        /// Searches for FireFox installations and enables trust of the local certificate store. 
        /// </summary>
        /// <remarks>
        /// If any profile is discovered that does not have the local CA cert store checking enabled
        /// already, all instances of firefox will be killed and then restarted when calling this method.
        /// </remarks>
        private void EstablishTrustWithFirefox()
        {
            // Get the default FireFox profiles path.
            string defaultFirefoxProfilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            defaultFirefoxProfilesPath += @"\Mozilla\Firefox\Profiles";

            if(!Directory.Exists(defaultFirefoxProfilesPath))
            {
                return;
            }

            // Figure out if firefox is running. If later it is and we kill it, store the path to
            // firefox.exe so we can restart the process after we're done.
            string firefoxExePath = string.Empty;
            bool firefoxIsRunning = Process.GetProcessesByName("firefox").Length > 0;

            var prefsFiles = Directory.GetFiles(defaultFirefoxProfilesPath, "prefs.js", SearchOption.AllDirectories);

            var valuesThatNeedToBeSet = new Dictionary<string, string>();

            var firefoxUserCfgValuesUri = "CitadelService.Resources.FireFoxUserCFG.txt";
            using(var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(firefoxUserCfgValuesUri))
            {
                if(resourceStream != null && resourceStream.CanRead)
                {
                    using(TextReader tsr = new StreamReader(resourceStream))
                    {
                        string cfgLine = null;
                        while((cfgLine = tsr.ReadLine()) != null)
                        {
                            if(cfgLine.Length > 0)
                            {
                                var firstSpace = cfgLine.IndexOf(' ');

                                if(firstSpace != -1)
                                {
                                    var key = cfgLine.Substring(0, firstSpace);
                                    var value = cfgLine.Substring(firstSpace);

                                    if(!valuesThatNeedToBeSet.ContainsKey(key))
                                    {
                                        valuesThatNeedToBeSet.Add(key, value);
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    m_logger.Error("Cannot read from firefox cfg resource file.");
                }
            }

            foreach(var prefFile in prefsFiles)
            {
                var userFile = Path.GetDirectoryName(prefFile) + Path.DirectorySeparatorChar + "user.js";

                string[] fileText = new string[0];

                if(File.Exists(userFile))
                {
                    fileText = File.ReadAllLines(prefFile);
                }

                var notFound = new Dictionary<string, string>();

                foreach(var kvp in valuesThatNeedToBeSet)
                {
                    var entryIndex = Array.FindIndex(fileText, l => l.StartsWith(kvp.Key));

                    if(entryIndex != -1)
                    {
                        if(!fileText[entryIndex].EndsWith(kvp.Value))
                        {
                            fileText[entryIndex] = kvp.Key + kvp.Value;
                            m_logger.Info("Firefox profile {0} has has preference {1}) adjusted to be set correctly already.", Directory.GetParent(prefFile).Name, kvp.Key);
                        }
                        else
                        {
                            m_logger.Info("Firefox profile {0} has preference {1}) set correctly already.", Directory.GetParent(prefFile).Name, kvp.Key);
                        }
                    }
                    else
                    {
                        notFound.Add(kvp.Key, kvp.Value);
                    }
                }

                var fileTextList = new List<string>(fileText);

                foreach(var nfk in notFound)
                {
                    m_logger.Info("Firefox profile {0} is having preference {1}) added.", Directory.GetParent(prefFile).Name, nfk.Key);
                    fileTextList.Add(nfk.Key + nfk.Value);
                }

                File.WriteAllLines(userFile, fileTextList);
            }

            // Always kill firefox.
            if(firefoxIsRunning)
            {
                // We need to kill firefox before editing the preferences, otherwise they'll just get overwritten.
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
        /// Called whenever a block action occurs. 
        /// </summary>
        /// <param name="category">
        /// The ID of the category that the blocked content was deemed to belong to. 
        /// </param>
        /// <param name="cause">
        /// The type of classification that led to the block action. 
        /// </param>
        /// <param name="requestUri">
        /// The URI of the request that was blocked or the request which generated the blocked response. 
        /// </param>
        /// <param name="matchingRule">
        /// The raw rule that caused the block action. May not be applicable for all block actions.
        /// Default is empty string.
        /// </param>
        private void OnRequestBlocked(short category, BlockType cause, Uri requestUri, string matchingRule = "")
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

            string categoryNameString = "Unknown";
            var mappedCategory = m_generatedCategoriesMap.Values.Where(xx => xx.CategoryId == category).FirstOrDefault();

            if(mappedCategory != null)
            {
                categoryNameString = mappedCategory.CategoryName;
            }

            m_ipcServer.NotifyBlockAction(cause, requestUri, categoryNameString, matchingRule);

            if(internetShutOff)
            {
                var restoreDate = DateTime.Now.AddTicks(m_config.ThresholdTimeoutPeriod.Ticks);

                var cooldownPeriod = (restoreDate - DateTime.Now);

                m_ipcServer.NotifyCooldownEnforced(cooldownPeriod);
            }

            m_logger.Info("Request {0} blocked by rule {1} in category {2}.", requestUri.ToString(), matchingRule, categoryNameString);
        }

        /// <summary>
        /// Called whenever the engine reports that elements were removed from the payload of a
        /// response to the given request.
        /// </summary>
        /// <param name="numElementsRemoved">
        /// The number of elements removed. 
        /// </param>
        /// <param name="fullRequest">
        /// The request who's response payload has had the elements removed. 
        /// </param>
        private void OnElementsBlocked(uint numElementsRemoved, string fullRequest)
        {
            Debug.WriteLine("Elements blocked.");
        }

        /// <summary>
        /// Called whenever the Engine want's to check if the application at the supplied absolute
        /// path should have its traffic forced through itself or not.
        /// </summary>
        /// <param name="appAbsolutePath">
        /// The absolute path to an application that the filter is inquiring about. 
        /// </param>
        /// <returns>
        /// True if the application at the specified absolute path should have its traffic forced
        /// through the filtering engine, false otherwise.
        /// </returns>
        private bool OnAppFirewallCheck(string appAbsolutePath)
        {
            // XXX TODO - The engine shouldn't even tell us about SYSTEM processes and just silently
            // let them through.
            if(appAbsolutePath.OIEquals("SYSTEM"))
            {
                return false;
            }

            // Lets completely avoid piping anything from the operating system in the filter, with
            // the sole exception of Microsoft edge.
            if((appAbsolutePath.IndexOf("MicrosoftEdge", StringComparison.OrdinalIgnoreCase) == -1) && appAbsolutePath.IndexOf(@"\Windows\", StringComparison.OrdinalIgnoreCase) != -1)
            {
                lock(s_foreverWhitelistedApplications)
                {
                    if(s_foreverWhitelistedApplications.Contains(appAbsolutePath))
                    {
                        return false;
                    }
                }

                // Here we'll simply check if the binary is signed. If so, we'll validate the
                // certificate. If the cert is good, let's just go and bypass this binary altogether.
                // However, note that this does not verify that the signed binary is actually valid
                // for the certificate. That is, it doesn't ensure file integrity. Also, note that
                // even if we went all the way as to use WinVerifyTrust() from wintrust.dll to
                // completely verify integrity etc, this can still be bypassed by adding a self
                // signed signing authority to the windows trusted certs.
                //
                // So, all we can do is kick the can further down the road. This should be sufficient
                // to prevent the lay person from dropping a browser into the Windows folder.
                //
                // Leaving above notes just for the sake of knowledge. We can kick the can pretty
                // darn far down the road by asking Windows Resource Protection if the file really
                // belongs to the OS. Viruses are known to call SfcIsFileProtected in order to avoid
                // getting caught messing with these files so if viruses avoid them, I think we've
                // booted the can so far down the road that we need not worry about being exploited
                // here. The OS would need to be funamentally compromised and that wouldn't be our fault.
                //
                // The only other way we could get exploited here by getting our hook to sfc.dll
                // hijacked. There are countermeasures of course but not right now.

                // If the result is greater than zero, then this is a protected operating system file
                // according to the operating system.
                if(SFC.SfcIsFileProtected(IntPtr.Zero, appAbsolutePath) > 0)
                {
                    lock(s_foreverWhitelistedApplications)
                    {
                        s_foreverWhitelistedApplications.Add(appAbsolutePath);
                    }

                    return false;
                }

                /* I don't think we need this at all anymore. Besides,
                 * many windows assemblies are not signed from what
                 * explorer is telling me.
                var cert = X509Certificate.CreateFromSignedFile(appAbsolutePath);
                if(cert != null)
                {
                    var cert2 = new X509Certificate2(cert.Handle);

                    if(cert2.Verify())
                    {
                    }
                }
                */
            }

            try
            {
                m_filteringRwLock.EnterReadLock();

                if(m_blacklistedApplications.Count == 0 && m_whitelistedApplications.Count == 0)
                {
                    // Just filter anything accessing port 80 and 443.
                    m_logger.Info("1Filtering application: {0}", appAbsolutePath);
                    return true;
                }

                var appName = Path.GetFileName(appAbsolutePath);

                if(m_whitelistedApplications.Count > 0)
                {
                    if(m_whitelistedApplications.Contains(appName))
                    {
                        // Whitelist is in effect and this app is whitelisted. So, don't force it through.
                        return false;
                    }

                    // Whitelist is in effect, and this app is not whitelisted, so force it through.
                    m_logger.Info("2Filtering application: {0}", appAbsolutePath);
                    return true;
                }

                if(m_blacklistedApplications.Count > 0)
                {
                    if(m_blacklistedApplications.Contains(appName))
                    {
                        // Blacklist is in effect and this app is blacklisted. So, force it through.
                        m_logger.Info("3Filtering application: {0}", appAbsolutePath);
                        return true;
                    }

                    // Blacklist in effect but this app is not on the blacklist. Don't force it through.
                    return false;
                }

                // This app was not hit by either an enforced whitelist or blacklist. So, by default we
                // will filter everything. We should never get here, but just in case.

                m_logger.Info("4Filtering application: {0}", appAbsolutePath);
                return true;
            }
            catch(Exception e)
            {
                m_logger.Error("Error in {0}", nameof(OnAppFirewallCheck));
                LoggerUtil.RecursivelyLogException(m_logger, e);
                return false;
            }
            finally
            {
                m_filteringRwLock.ExitReadLock();
            }
        }

        private void OnHttpMessageBegin(string requestHeaders, byte[] requestPayload, string responseHeaders, byte[] responsePayload, out Engine.ProxyNextAction nextAction, out byte[] customBlockResponseData)
        {
            nextAction = Engine.ProxyNextAction.AllowAndIgnoreContent;
            customBlockResponseData = null;

            bool readLocked = false;

            try
            {
                // It should be fine, for our purposes, to just smash both response and request
                // headers together. We're only interested, in all of our filtering code, in headers
                // that are unique to requests or responses, so this should be fine.
                var parsedHeaders = ParseHeaders(requestHeaders + "\r\n" + responseHeaders);

                Uri requestUri = null;
                string httpVersion = null;

                if(!parsedHeaders.TryGetRequestUri(out requestUri))
                {
                    // Don't bother logging this. This is just google chrome being stupid with URI's for new tabs.
                    //m_logger.Error("Malformed headers in OnHttpMessageBegin. Missing request URI.");
                    return;
                }

                if(!parsedHeaders.TryGetHttpVersion(out httpVersion))
                {
                    // Don't bother logging this. This is just google chrome being stupid with URI's for new tabs.
                    //m_logger.Error("Malformed headers in OnHttpMessageBegin. Missing http version declaration.");
                    return;
                }

                if(m_filterCollection != null)
                {
                    readLocked = true;
                    m_filteringRwLock.EnterReadLock();

                    var filters = m_filterCollection.GetWhitelistFiltersForDomain(requestUri.Host).Result;
                    short matchCategory = -1;
                    UrlFilter matchingFilter = null;

                    if(CheckIfFiltersApply(filters, requestUri, parsedHeaders, out matchingFilter, out matchCategory))
                    {
                        var mappedCategory = m_generatedCategoriesMap.Values.Where(xx => xx.CategoryId == matchCategory).FirstOrDefault();

                        if(mappedCategory != null)
                        {
                            m_logger.Info("Request {0} whitelisted by rule {1} in category {2}.", requestUri.ToString(), matchingFilter.OriginalRule, mappedCategory.CategoryName);
                        }
                        else
                        {
                            m_logger.Info("Request {0} whitelisted by rule {1} in category {2}.", requestUri.ToString(), matchingFilter.OriginalRule, matchCategory);
                        }

                        nextAction = Engine.ProxyNextAction.AllowAndIgnoreContentAndResponse;
                        return;
                    }

                    filters = m_filterCollection.GetWhitelistFiltersForDomain().Result;

                    if(CheckIfFiltersApply(filters, requestUri, parsedHeaders, out matchingFilter, out matchCategory))
                    {
                        var mappedCategory = m_generatedCategoriesMap.Values.Where(xx => xx.CategoryId == matchCategory).FirstOrDefault();

                        if(mappedCategory != null)
                        {
                            m_logger.Info("Request {0} whitelisted by rule {1} in category {2}.", requestUri.ToString(), matchingFilter.OriginalRule, mappedCategory.CategoryName);
                        }
                        else
                        {
                            m_logger.Info("Request {0} whitelisted by rule {1} in category {2}.", requestUri.ToString(), matchingFilter.OriginalRule, matchCategory);
                        }

                        nextAction = Engine.ProxyNextAction.AllowAndIgnoreContentAndResponse;
                        return;
                    }

                    filters = m_filterCollection.GetFiltersForDomain(requestUri.Host).Result;

                    if(CheckIfFiltersApply(filters, requestUri, parsedHeaders, out matchingFilter, out matchCategory))
                    {
                        OnRequestBlocked(matchCategory, BlockType.Url, requestUri, matchingFilter.OriginalRule);
                        nextAction = Engine.ProxyNextAction.DropConnection;
                        customBlockResponseData = GetBlockedResponse(httpVersion, true);
                        return;
                    }

                    filters = m_filterCollection.GetFiltersForDomain().Result;

                    if(CheckIfFiltersApply(filters, requestUri, parsedHeaders, out matchingFilter, out matchCategory))
                    {
                        OnRequestBlocked(matchCategory, BlockType.Url, requestUri, matchingFilter.OriginalRule);
                        nextAction = Engine.ProxyNextAction.DropConnection;
                        customBlockResponseData = GetBlockedResponse(httpVersion, true);
                        return;
                    }
                }

                string contentType = null;
                if((contentType = parsedHeaders["Content-Type"]) != null)
                {
                    m_logger.Info("Got content type {0} in {1}", contentType, nameof(OnHttpMessageBegin));

                    // This is the start of a response with a content type that we want to inspect.
                    // Flag it for inspection once done. It will later call the OnHttpMessageEnd callback.
                    var isHtml = contentType.IndexOf("html") != -1;
                    var isJson = contentType.IndexOf("json") != -1;
                    if(isHtml || isJson)
                    {
                        nextAction = Engine.ProxyNextAction.AllowButRequestContentInspection;
                        customBlockResponseData = null;
                        return;
                    }
                }
            }
            catch(Exception e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
            finally
            {
                if(readLocked)
                {
                    m_filteringRwLock.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// This callback is invoked whenever a http transaction has fully completed. The only time
        /// we should ever be called here is when we've flagged a transaction to keep all content for
        /// inspection. This could be a request or a response, it doesn't really matter.
        /// </summary>
        /// <param name="requestHeaders">
        /// The headers for the request that this callback is being invoked for. 
        /// </param>
        /// <param name="requestPayload">
        /// The payload of the request that this callback is being invoked for. May be empty. Either
        /// this, or the response payload should NOT be empty.
        /// </param>
        /// <param name="responseHeaders">
        /// The headers for the response that this callback is being invoked for. May be empty, as
        /// this might be a request only.
        /// </param>
        /// <param name="responsePayload">
        /// The payload of the response that this callback is being invoked for. May be empty. Either
        /// this, or the request payload should NOT be empty. Either or both. If this array is not
        /// empty, then you're being asked to filter a response payload.
        /// </param>
        /// <param name="shouldBlock">
        /// An out param where we can specify if this content should be blocked or not. 
        /// </param>
        /// <param name="customBlockResponseData">
        /// </param>
        private void OnHttpMessageEnd(string requestHeaders, byte[] requestPayload, string responseHeaders, byte[] responsePayload, out bool shouldBlock, out byte[] customBlockResponseData)
        {
            shouldBlock = false;
            customBlockResponseData = null;

            m_logger.Info("Entering {0}", nameof(OnHttpMessageEnd));

            bool requestIsNull = (requestPayload == null || requestPayload.Length == 0);
            bool responseIsNull = (responsePayload == null || responsePayload.Length == 0);

            if(requestIsNull && responseIsNull)
            {
                m_logger.Info("All payloads are null in {0}. Cannot inspect.", nameof(OnHttpMessageEnd));
                return;
            }

            byte[] whichPayload = responseIsNull ? requestPayload : responsePayload;

            // The only thing we can really do in this callback, and the only thing we care to do, is
            // try to classify the content of the response payload, if there is any.
            try
            {
                var parsedHeaders = ParseHeaders(requestHeaders + "\r\n" + responseHeaders);

                Uri requestUri = null;

                if(!parsedHeaders.TryGetRequestUri(out requestUri))
                {
                    // Don't bother logging this. This is just google chrome being stupid with URI's for new tabs.
                    //m_logger.Error("Malformed headers in OnHttpMessageBegin. Missing request URI.");
                    return;
                }

                string contentType = null;
                string httpVersion = null;

                if((contentType = parsedHeaders["Content-Type"]) != null)
                {
                    contentType = contentType.ToLower();

                    foreach(string key in parsedHeaders)
                    {
                        string val = parsedHeaders[key];
                        if(val == null || val == string.Empty)
                        {
                            var si = key.IndexOf(' ');
                            if(si != -1)
                            {
                                var sub = key.Substring(0, si);

                                if(sub.StartsWith("HTTP/"))
                                {
                                    httpVersion = sub.Substring(5);
                                    break;
                                }
                            }
                        }
                    }

                    httpVersion = httpVersion != null ? httpVersion : "1.1";

                    BlockType blockType;
                    var contentClassResult = OnClassifyContent(whichPayload, contentType, out blockType);

                    if(contentClassResult > 0)
                    {
                        shouldBlock = true;
                        customBlockResponseData = GetBlockedResponse(httpVersion, contentType.IndexOf("html") == -1);
                        OnRequestBlocked(contentClassResult, blockType, requestUri);
                        m_logger.Info("Response blocked by content classification.");
                    }
                }
            }
            catch(Exception e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
        }

        private bool CheckIfFiltersApply(List<UrlFilter> filters, Uri request, NameValueCollection headers, out UrlFilter matched, out short matchedCategory)
        {
            matchedCategory = -1;
            matched = null;

            var len = filters.Count;
            for(int i = 0; i < len; ++i)
            {
                Console.WriteLine(filters[i].IsException);
                if(m_categoryIndex.GetIsCategoryEnabled(filters[i].CategoryId) && filters[i].IsMatch(request, headers))
                {
                    matched = filters[i];
                    matchedCategory = filters[i].CategoryId;
                    return true;
                }
            }

            return false;
        }

        private byte[] GetBlockedResponse(string httpVersion = "1.1", bool noContent = false)
        {
            switch(noContent)
            {
                default:
                case false:
                {
                    return Encoding.UTF8.GetBytes(string.Format("HTTP/{0} 204 No Content\r\nDate: {1}\r\nExpires: {2}\n\nContent-Length: 0\r\n\r\n", httpVersion, DateTime.UtcNow.ToString("r"), s_EpochHttpDateTime));
                }

                case true:
                {
                    return Encoding.UTF8.GetBytes(string.Format("HTTP/{0} 20O OK\r\nDate: {1}\r\nExpires: {2}\r\nContent-Type: text/html\r\nContent-Length: {3}\r\n\r\n{4}\r\n\r\n", httpVersion, DateTime.UtcNow.ToString("r"), s_EpochHttpDateTime, m_blockedHtmlPage.Length, m_blockedHtmlPage));
                }
            }
        }

        private NameValueCollection ParseHeaders(string headers)
        {
            var nvc = new NameValueCollection(StringComparer.OrdinalIgnoreCase);

            using(var reader = new StringReader(headers))
            {
                string line = null;
                while((line = reader.ReadLine()) != null)
                {
                    if(string.IsNullOrEmpty(line) || string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    var firstSplitIndex = line.IndexOf(':');
                    if(firstSplitIndex == -1)
                    {
                        nvc.Add(line.Trim(), string.Empty);
                    }
                    else
                    {
                        nvc.Add(line.Substring(0, firstSplitIndex).Trim(), line.Substring(firstSplitIndex + 1).Trim());
                    }
                }
            }

            return nvc;
        }

        /// <summary>
        /// Called by the engine when the engine fails to classify a request or response by its
        /// metadata. The engine provides a full byte array of the content of the request or
        /// response, along with the declared content type of the data. This is currently used for
        /// NLP classification, but can be adapted with minimal changes to the Engine.
        /// </summary>
        /// <param name="data">
        /// The data to be classified. 
        /// </param>
        /// <param name="contentType">
        /// The declared content type of the data. 
        /// </param>
        /// <returns>
        /// A numeric category ID that the content was deemed to belong to. Zero is returned here if
        /// the content is not deemed to be part of any known category, which is a general indication
        /// to the engine that the content should not be blocked.
        /// </returns>
        private short OnClassifyContent(byte[] data, string contentType, out BlockType blockedBecause)
        {
            try
            {
                m_filteringRwLock.EnterReadLock();

                if(m_textTriggers != null && m_textTriggers.HasTriggers)
                {
                    var isHtml = contentType.IndexOf("html") != -1;
                    var isJson = contentType.IndexOf("json") != -1;
                    if(isHtml || isJson)
                    {
                        var dataToAnalyzeStr = Encoding.UTF8.GetString(data);

                        if(isHtml)
                        {
                            var ext = new FastHtmlTextExtractor();
                            dataToAnalyzeStr = ext.Extract(dataToAnalyzeStr.ToCharArray(), true);
                        }

                        short matchedCategory = -1;
                        string trigger = null;
                        if(m_textTriggers.ContainsTrigger(dataToAnalyzeStr, out matchedCategory, out trigger, m_categoryIndex.GetIsCategoryEnabled, isHtml, m_config != null ? m_config.MaxTextTriggerScanningSize : -1))
                        {
                            var mappedCategory = m_generatedCategoriesMap.Values.Where(xx => xx.CategoryId == matchedCategory).FirstOrDefault();

                            if(mappedCategory != null)
                            {
                                m_logger.Info("Response blocked by text trigger \"{0}\" in category {1}.", trigger, mappedCategory.CategoryName);
                                blockedBecause = BlockType.TextTrigger;
                                return mappedCategory.CategoryId;
                            }
                        }
                    }
                }
            }
            catch(Exception e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
            finally
            {
                m_filteringRwLock.ExitReadLock();
            }

            try
            {
                m_doccatSlimLock.EnterReadLock();

                contentType = contentType.ToLower();

                // Only attempt text classification if we have a text classifier, silly.
                if(m_documentClassifiers != null && m_documentClassifiers.Count > 0)
                {
                    var textToClassifyBuilder = new StringBuilder();

                    if(contentType.IndexOf("html") != -1)
                    {
                        // This might be plain text, might be HTML. We need to find out.
                        var rawText = Encoding.UTF8.GetString(data).ToCharArray();

                        var extractor = new FastHtmlTextExtractor();

                        var extractedText = extractor.Extract(rawText);
                        m_logger.Info("From HTML: Classify this string: {0}", extractedText);
                        textToClassifyBuilder.Append(extractedText);
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
                        foreach(var classifier in m_documentClassifiers)
                        {
                            m_logger.Info("Got text to classify of length {0}.", textToClassify.Length);

                            // Remove all multi-whitespace, newlines etc.
                            textToClassify = m_whitespaceRegex.Replace(textToClassify, " ");

                            var classificationResult = classifier.ClassifyText(textToClassify);

                            MappedFilterListCategoryModel categoryNumber = null;

                            if(m_generatedCategoriesMap.TryGetValue(classificationResult.BestCategoryName, out categoryNumber))
                            {
                                if(categoryNumber.CategoryId > 0 && m_categoryIndex.GetIsCategoryEnabled(categoryNumber.CategoryId))
                                {
                                    var threshold = m_config != null ? m_config.NlpThreshold : 0.9f;

                                    if(classificationResult.BestCategoryScore < threshold)
                                    {
                                        m_logger.Info("Rejected {0} classification because score was less than threshold of {1}. Returned score was {2}.", classificationResult.BestCategoryName, threshold, classificationResult.BestCategoryScore);
                                        blockedBecause = BlockType.OtherContentClassification;
                                        return 0;
                                    }

                                    m_logger.Info("Classified text content as {0}.", classificationResult.BestCategoryName);
                                    blockedBecause = BlockType.TextClassification;
                                    return categoryNumber.CategoryId;
                                }
                            }
                            else
                            {
                                m_logger.Info("Did not find category registered: {0}.", classificationResult.BestCategoryName);
                            }
                        }
                    }
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
            blockedBecause = BlockType.OtherContentClassification;
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
            // Reset count to zero.
            Interlocked.Exchange(ref m_thresholdTicks, 0);

            this.m_thresholdCountTimer.Change(m_config.ThresholdTriggerPeriod, Timeout.InfiniteTimeSpan);
        }

        /// <summary>
        /// Called whenever the threshold timeout period has elapsed. Here we'll restore internet access. 
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

            Status = FilterStatus.Running;

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
            try
            {
                Status = FilterStatus.Synchronizing;

                m_logger.Info("Checking for filter list updates.");

                m_updateRwLock.EnterWriteLock();

                bool gotUpdatedFilterLists = UpdateListData();

                if(gotUpdatedFilterLists)
                {
                    // Got new data. Gotta reload.
                    ReloadFilteringRules();
                }

                m_logger.Info("Checking for application updates.");

                // Check for app updates.
                ProbeMasterForApplicationUpdates();
            }
            catch(Exception e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
            finally
            {
                // Enable the timer again.
                if(!WebServiceUtil.GetHasInternetServiceAsync())
                {
                    // If we have no internet, keep polling every 15 seconds. We need that data ASAP.
                    this.m_updateCheckTimer.Change(TimeSpan.FromSeconds(15), Timeout.InfiniteTimeSpan);
                }
                else
                {
                    if(m_config != null)
                    {
                        this.m_updateCheckTimer.Change(m_config.UpdateFrequency, Timeout.InfiniteTimeSpan);
                    }
                    else
                    {
                        this.m_updateCheckTimer.Change(TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);
                    }
                }

                m_updateRwLock.ExitWriteLock();

                // Handled, so give the user back their normal UI.
                Status = FilterStatus.Running;
            }

            try
            {
                // Challenge user authenticity
                ChallengeUserAuthenticity();
            }
            catch(Exception e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
        }

        /// <summary>
        /// Starts the filtering engine. 
        /// </summary>
        private void StartFiltering()
        {
            m_logger.Info(nameof(StartFiltering));
            // Let's make sure we've pulled our internet block.
            try
            {
                WFPUtility.EnableInternet();
            }
            catch { }

            try
            {
                if(m_filteringEngine != null && !m_filteringEngine.IsRunning)
                {
                    m_logger.Info("Start engine.");

                    // Start the engine right away, to ensure the atomic bool IsRunning is set.
                    m_filteringEngine.Start();

                    // Make sure our lists are up to date and try to update the app, etc etc. Just
                    // call our update timer complete handler manually.
                    OnUpdateTimerElapsed(null);

                    ReloadFilteringRules();
                }
            }
            catch(Exception e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
        }

        /// <summary>
        /// Queries the service provider for updated filtering rules. 
        /// </summary>
        private void ReloadFilteringRules()
        {
            try
            {
                m_filteringRwLock.EnterWriteLock();

                // Load our configuration file and load configured lists, etc.
                var configDataFilePath = AppDomain.CurrentDomain.BaseDirectory + "a.dat";

                if(File.Exists(configDataFilePath))
                {
                    using(var file = File.OpenRead(configDataFilePath))
                    {
                        using(var zip = new ZipArchive(file, ZipArchiveMode.Read))
                        {
                            // Find the configuration JSON file.
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

                            // Deserialize config
                            try
                            {
                                m_config = JsonConvert.DeserializeObject<AppConfigModel>(cfgJson, m_configSerializerSettings);
                            }
                            catch(Exception deserializationError)
                            {
                                m_logger.Error("Failed to deserialize JSON config.");
                                LoggerUtil.RecursivelyLogException(m_logger, deserializationError);
                                return;
                            }

                            if(m_config.UpdateFrequency.Minutes <= 0 || m_config.UpdateFrequency == Timeout.InfiniteTimeSpan)
                            {
                                // Just to ensure that we enforce a minimum value here.
                                m_config.UpdateFrequency = TimeSpan.FromMinutes(5);
                            }

                            // Enforce DNS if present.
                            TryEnfornceDns();

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

                            // Setup blocking threshold, anti-tamper mechamisms etc.
                            if(m_config.UseThreshold)
                            {
                                // Setup the threshold timers and related data members.
                                InitThresholdData();
                            }

                            if(m_config.CannotTerminate)
                            {
                                // Turn on process protection if requested.
                                ProcessProtection.Protect();
                            }

                            // XXX FIXME Update our dashboard view model if there are bypasses
                            // configured. Force this up to the UI thread because it's a UI model.
                            if(m_config.BypassesPermitted > 0)
                            {
                                m_ipcServer.NotifyRelaxedPolicyChange(m_config.BypassesPermitted, m_config.BypassDuration);
                            }
                            else
                            {
                                m_ipcServer.NotifyRelaxedPolicyChange(0, TimeSpan.Zero);
                            }

                            // Recreate our filter collection and reset all categories to be disabled.
                            if(m_filterCollection != null)
                            {
                                m_filterCollection.Dispose();
                            }

                            // Recreate our triggers container.
                            if(m_textTriggers != null)
                            {
                                m_textTriggers.Dispose();
                            }

                            // We need to force clearing of all connection pools, then force a
                            // shutdown on the native side of our SQlite managed wrapper in order to
                            // force connections to existing databases to be destroyed. This is
                            // primarily a concern for in-memory databases, because without this
                            // code, those in memory db's will persist so long as any connection is
                            // left open to them.
                            try
                            {
                                SQLiteConnection.ClearAllPools();
                            }
                            catch { }

                            try
                            {
                                SQLiteConnection.Shutdown(true, true);
                            }
                            catch { }

                            m_filterCollection = new FilterDbCollection(AppDomain.CurrentDomain.BaseDirectory + "rules.db", true, true);
                            m_categoryIndex.SetAll(false);

                            // XXX TODO - Maybe make it a compiler flag to toggle if this is going to
                            // be an in-memory DB or not.
                            m_textTriggers = new BagOfTextTriggers(AppDomain.CurrentDomain.BaseDirectory + "t.dat", true, true);

                            // Now clear all generated categories. These will be re-generated as needed.
                            m_generatedCategoriesMap.Clear();

                            // Now drop all existing NLP models.
                            try
                            {
                                m_doccatSlimLock.EnterWriteLock();
                                m_documentClassifiers.Clear();
                            }
                            finally
                            {
                                m_doccatSlimLock.ExitWriteLock();
                            }

                            // Load all configured NLP models.
                            foreach(var nlpEntry in m_config.ConfiguredNlpModels)
                            {
                                var modelEntry = zip.Entries.Where(pp => pp.FullName.OIEquals(nlpEntry.RelativeModelPath)).FirstOrDefault();
                                if(modelEntry != null)
                                {
                                    using(var mStream = modelEntry.Open())
                                    using(var ms = new MemoryStream())
                                    {
                                        mStream.CopyTo(ms);
                                        LoadNlpModel(ms.ToArray(), nlpEntry);
                                    }
                                }
                            }

                            uint totalFilterRulesLoaded = 0;
                            uint totalFilterRulesFailed = 0;
                            uint totalTriggersLoaded = 0;

                            // Load all configured list files.
                            foreach(var listModel in m_config.ConfiguredLists)
                            {
                                var listEntry = zip.Entries.Where(pp => pp.FullName.OIEquals(listModel.RelativeListPath)).FirstOrDefault();
                                if(listEntry != null)
                                {
                                    var thisListCategoryName = listModel.RelativeListPath.Substring(0, listModel.RelativeListPath.LastIndexOfAny(new[] { '/', '\\' }) + 1) + Path.GetFileNameWithoutExtension(listModel.RelativeListPath);

                                    MappedFilterListCategoryModel categoryModel = null;

                                    switch(listModel.ListType)
                                    {
                                        case PlainTextFilteringListType.Blacklist:
                                        {
                                            if(TryFetchOrCreateCategoryMap(thisListCategoryName, out categoryModel))
                                            {
                                                using(var listStream = listEntry.Open())
                                                {
                                                    var loadedFailedRes = m_filterCollection.ParseStoreRulesFromStream(listStream, categoryModel.CategoryId).Result;
                                                    totalFilterRulesLoaded += (uint)loadedFailedRes.Item1;
                                                    totalFilterRulesFailed += (uint)loadedFailedRes.Item2;

                                                    if(loadedFailedRes.Item1 > 0)
                                                    {
                                                        m_categoryIndex.SetIsCategoryEnabled(categoryModel.CategoryId, true);
                                                    }
                                                }
                                            }
                                        }
                                        break;

                                        case PlainTextFilteringListType.BypassList:
                                        {
                                            MappedBypassListCategoryModel bypassCategoryModel = null;

                                            // Must be loaded twice. Once as a blacklist, once as a whitelist.
                                            if(TryFetchOrCreateCategoryMap(thisListCategoryName, out bypassCategoryModel))
                                            {
                                                // Load first as blacklist.
                                                using(var listStream = listEntry.Open())
                                                {
                                                    var loadedFailedRes = m_filterCollection.ParseStoreRulesFromStream(listStream, bypassCategoryModel.CategoryId).Result;
                                                    totalFilterRulesLoaded += (uint)loadedFailedRes.Item1;
                                                    totalFilterRulesFailed += (uint)loadedFailedRes.Item2;

                                                    if(loadedFailedRes.Item1 > 0)
                                                    {
                                                        m_categoryIndex.SetIsCategoryEnabled(bypassCategoryModel.CategoryId, true);
                                                    }
                                                }

                                                GC.Collect();

                                                // Load second as whitelist, but start off with the
                                                // category disabled.
                                                using(TextReader tr = new StreamReader(listEntry.Open()))
                                                {
                                                    var bypassAsWhitelistRules = new List<string>();
                                                    string line = null;
                                                    while((line = tr.ReadLine()) != null)
                                                    {
                                                        bypassAsWhitelistRules.Add("@@" + line.Trim() + "\n");
                                                    }

                                                    var loadedFailedRes = m_filterCollection.ParseStoreRules(bypassAsWhitelistRules.ToArray(), bypassCategoryModel.CategoryIdAsWhitelist).Result;
                                                    totalFilterRulesLoaded += (uint)loadedFailedRes.Item1;
                                                    totalFilterRulesFailed += (uint)loadedFailedRes.Item2;

                                                    m_categoryIndex.SetIsCategoryEnabled(bypassCategoryModel.CategoryIdAsWhitelist, false);
                                                }

                                                GC.Collect();
                                            }
                                        }
                                        break;

                                        case PlainTextFilteringListType.TextTrigger:
                                        {
                                            // Always load triggers as blacklists.
                                            if(TryFetchOrCreateCategoryMap(thisListCategoryName, out categoryModel))
                                            {
                                                using(var listStream = listEntry.Open())
                                                {
                                                    var triggersLoaded = m_textTriggers.LoadStoreFromStream(listStream, categoryModel.CategoryId).Result;
                                                    m_textTriggers.FinalizeForRead();

                                                    totalTriggersLoaded += (uint)triggersLoaded;

                                                    if(triggersLoaded > 0)
                                                    {
                                                        m_categoryIndex.SetIsCategoryEnabled(categoryModel.CategoryId, true);
                                                    }
                                                }
                                            }

                                            GC.Collect();
                                        }
                                        break;

                                        case PlainTextFilteringListType.Whitelist:
                                        {
                                            using(TextReader tr = new StreamReader(listEntry.Open()))
                                            {
                                                if(TryFetchOrCreateCategoryMap(thisListCategoryName, out categoryModel))
                                                {
                                                    var whitelistRules = new List<string>();
                                                    string line = null;
                                                    while((line = tr.ReadLine()) != null)
                                                    {
                                                        whitelistRules.Add("@@" + line.Trim() + "\n");
                                                    }

                                                    using(var listStream = listEntry.Open())
                                                    {
                                                        var loadedFailedRes = m_filterCollection.ParseStoreRules(whitelistRules.ToArray(), categoryModel.CategoryId).Result;
                                                        totalFilterRulesLoaded += (uint)loadedFailedRes.Item1;
                                                        totalFilterRulesFailed += (uint)loadedFailedRes.Item2;

                                                        if(loadedFailedRes.Item1 > 0)
                                                        {
                                                            m_categoryIndex.SetIsCategoryEnabled(categoryModel.CategoryId, true);
                                                        }
                                                    }
                                                }
                                            }

                                            GC.Collect();
                                        }
                                        break;
                                    }
                                }
                            }

                            m_logger.Info("Loaded {0} rules, {1} rules failed most likely due to being malformed, and {2} text triggers loaded.", totalFilterRulesLoaded, totalFilterRulesFailed, totalTriggersLoaded);
                        }
                    }
                }

                if(m_config != null)
                {
                    // Put the new update frequence into effect.
                    this.m_updateCheckTimer.Change(m_config.UpdateFrequency, Timeout.InfiniteTimeSpan);
                }
            }
            catch(Exception e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
            finally
            {
                m_filteringRwLock.ExitWriteLock();
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
                if(entry is MappedBypassListCategoryModel)
                {
                    m_categoryIndex.SetIsCategoryEnabled(((MappedBypassListCategoryModel)entry).CategoryIdAsWhitelist, true);
                }
            }

            m_relaxedPolicyExpiryTimer.Change(m_config.BypassDuration, Timeout.InfiniteTimeSpan);

            DecrementRelaxedPolicy();
        }

        private void DecrementRelaxedPolicy()
        {
            bool allUsesExhausted = false;

            if(m_config != null)
            {
                m_config.BypassesUsed++;

                allUsesExhausted = m_config.BypassesPermitted - m_config.BypassesUsed <= 0;

                if(allUsesExhausted)
                {
                    m_ipcServer.NotifyRelaxedPolicyChange(0, TimeSpan.Zero);
                }
                else
                {
                    m_ipcServer.NotifyRelaxedPolicyChange(m_config.BypassesPermitted - m_config.BypassesUsed, m_config.BypassDuration);
                }
            }
            else
            {
                m_ipcServer.NotifyRelaxedPolicyChange(0, TimeSpan.Zero);
            }

            if(allUsesExhausted)
            {
                // Refresh tomorrow at midnight.
                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);
                var span = tomorrow - DateTime.Now;

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
                if(entry is MappedBypassListCategoryModel)
                {
                    if(m_categoryIndex.GetIsCategoryEnabled(((MappedBypassListCategoryModel)entry).CategoryIdAsWhitelist) == true)
                    {
                        relaxedInEffect = true;
                    }
                }
            }

            // Ensure timer is stopped and re-enable categories by simply calling the timer's expiry callback.
            if(relaxedInEffect)
            {
                OnRelaxedPolicyTimerExpired(null);
            }

            // If a policy was not already in effect, then the user is choosing to relinquish a
            // policy not yet used. So just eat it up. If this is not the case, then the policy has
            // already been decremented, so don't bother.
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
            // Enable every category that is a bypass category.
            foreach(var entry in m_generatedCategoriesMap.Values)
            {
                if(entry is MappedBypassListCategoryModel)
                {
                    m_categoryIndex.SetIsCategoryEnabled(((MappedBypassListCategoryModel)entry).CategoryIdAsWhitelist, false);
                }
            }

            // Disable the expiry timer.
            m_relaxedPolicyExpiryTimer.Change(Timeout.Infinite, Timeout.Infinite);
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
            if(m_config != null)
            {
                m_config.BypassesUsed = 0;
                m_ipcServer.NotifyRelaxedPolicyChange(m_config.BypassesPermitted, m_config.BypassDuration);
            }

            // Disable the reset timer.
            m_relaxedPolicyResetTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Attempts to read DNS configuration data from the application configuration and then set
        /// those DNS settings on all available non-tunnel adapters.
        /// </summary>
        /// <param name="state">
        /// State object for timer. Always null, unused. 
        /// </param>
        private void TryEnfornceDns(object state = null)
        {
            lock(m_dnsEnforcementLock)
            {
                try
                {
                    IPAddress primaryDns = null;
                    IPAddress secondaryDns = null;
                    // Check if any DNS servers are defined, and if so, set them.
                    if(StringExtensions.Valid(m_config.PrimaryDns))
                    {
                        IPAddress.TryParse(m_config.PrimaryDns.Trim(), out primaryDns);
                    }

                    if(StringExtensions.Valid(m_config.SecondaryDns))
                    {
                        IPAddress.TryParse(m_config.SecondaryDns.Trim(), out secondaryDns);
                    }

                    if(primaryDns != null || secondaryDns != null)
                    {
                        var setDnsForNic = new Action<string, IPAddress, IPAddress>((nicName, pDns, sDns) =>
                        {
                            using(var networkConfigMng = new ManagementClass("Win32_NetworkAdapterConfiguration"))
                            {
                                using(var networkConfigs = networkConfigMng.GetInstances())
                                {
                                    foreach(var managementObject in networkConfigs.Cast<ManagementObject>().Where(objMO => (bool)objMO["IPEnabled"] && objMO["Description"].Equals(nicName)))
                                    {
                                        using(var newDNS = managementObject.GetMethodParameters("SetDNSServerSearchOrder"))
                                        {
                                            List<string> dnsServers = new List<string>();
                                            var existingDns = (string[])newDNS["DNSServerSearchOrder"];
                                            if(existingDns != null && existingDns.Length > 0)
                                            {
                                                dnsServers = new List<string>(existingDns);
                                            }

                                            bool changed = false;

                                            if(pDns != null)
                                            {
                                                if(!dnsServers.Contains(pDns.ToString()))
                                                {
                                                    dnsServers.Insert(0, pDns.ToString());
                                                    changed = true;
                                                }
                                            }
                                            if(sDns != null)
                                            {
                                                if(!dnsServers.Contains(sDns.ToString()))
                                                {
                                                    changed = true;

                                                    if(dnsServers.Count > 0)
                                                    {
                                                        dnsServers.Insert(1, sDns.ToString());
                                                    }
                                                    else
                                                    {
                                                        dnsServers.Add(sDns.ToString());
                                                    }
                                                }
                                            }

                                            if(changed)
                                            {
                                                m_logger.Info("Setting DNS for adapter {1} to {0}.", nicName, string.Join(",", dnsServers.ToArray()));
                                                newDNS["DNSServerSearchOrder"] = dnsServers.ToArray();
                                                managementObject.InvokeMethod("SetDNSServerSearchOrder", newDNS, null);
                                            }
                                            else
                                            {
                                                m_logger.Info("No change in DNS settings.");
                                            }
                                        }
                                    }
                                }
                            }
                        });

                        var ifaces = NetworkInterface.GetAllNetworkInterfaces().Where(x => x.OperationalStatus == OperationalStatus.Up && x.NetworkInterfaceType != NetworkInterfaceType.Tunnel);

                        foreach(var iface in ifaces)
                        {
                            bool needsUpdate = false;

                            if(primaryDns != null && !iface.GetIPProperties().DnsAddresses.Contains(primaryDns))
                            {
                                needsUpdate = true;
                            }
                            if(secondaryDns != null && !iface.GetIPProperties().DnsAddresses.Contains(secondaryDns))
                            {
                                needsUpdate = true;
                            }

                            if(needsUpdate)
                            {
                                setDnsForNic(iface.Description, primaryDns, secondaryDns);
                            }
                        }
                    }
                }
                catch(Exception e)
                {
                    LoggerUtil.RecursivelyLogException(m_logger, e);
                }

                if(m_dnsEnforcementTimer == null)
                {
                    m_dnsEnforcementTimer = new Timer(TryEnfornceDns, null, Timeout.Infinite, Timeout.Infinite);
                }
                else
                {
                    m_dnsEnforcementTimer.Change(TimeSpan.FromMinutes(1), Timeout.InfiniteTimeSpan);
                }
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
        /// Attempts to fetch a FilterListEntry instance for the supplied category name, or create a
        /// new one if one does not exist. Whether one is created, or an existing instance is
        /// discovered, a valid, unique FilterListEntry for the supplied category shall be returned.
        /// </summary>
        /// <param name="categoryName">
        /// The category name for which to fetch or generate a new FilterListEntry instance. 
        /// </param>
        /// <returns>
        /// The unique FilterListEntry for the supplied category name, whether an existing instance
        /// was found or a new one was created.
        /// </returns>
        /// <remarks>
        /// This will always fail if more than 255 categories are created! 
        /// </remarks>
        private bool TryFetchOrCreateCategoryMap<T>(string categoryName, out T model) where T : MappedFilterListCategoryModel
        {
            m_logger.Info("CATEGORY {0}", categoryName);

            MappedFilterListCategoryModel existingCategory = null;
            if(!m_generatedCategoriesMap.TryGetValue(categoryName, out existingCategory))
            {
                // We can't generate anymore categories. Sorry, but the rest get ignored.
                if(m_generatedCategoriesMap.Count >= short.MaxValue)
                {
                    m_logger.Error("The maximum number of filtering categories has been exceeded.");
                    model = null;
                    return false;
                }

                if(typeof(T) == typeof(MappedBypassListCategoryModel))
                {
                    MappedFilterListCategoryModel secondCategory = null;

                    if(TryFetchOrCreateCategoryMap(categoryName + "_as_whitelist", out secondCategory))
                    {
                        var newModel = (T)(MappedFilterListCategoryModel)new MappedBypassListCategoryModel((byte)((m_generatedCategoriesMap.Count) + 1), secondCategory.CategoryId, categoryName, secondCategory.CategoryName);
                        m_generatedCategoriesMap.GetOrAdd(categoryName, newModel);
                        model = newModel;
                        return true;
                    }
                    else
                    {
                        model = null;
                        return false;
                    }
                }
                else
                {
                    var newModel = (T)new MappedFilterListCategoryModel((byte)((m_generatedCategoriesMap.Count) + 1), categoryName);
                    m_generatedCategoriesMap.GetOrAdd(categoryName, newModel);
                    model = newModel;
                    return true;
                }
            }

            model = existingCategory as T;
            return true;
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
                    m_ipcServer.Dispose();

                    try
                    {
                        // Pull our critical status.
                        ProcessProtection.Unprotect();
                    }
                    catch(Exception e)
                    {
                        LoggerUtil.RecursivelyLogException(m_logger, e);
                    }

                    try
                    {
                        // Shut down engine.
                        StopFiltering();
                    }
                    catch(Exception e)
                    {
                        LoggerUtil.RecursivelyLogException(m_logger, e);
                    }

                    if(installSafeguards)
                    {
                        try
                        {
                            // Ensure we're automatically running at startup.
                            var scProcNfo = new ProcessStartInfo("sc.exe");
                            scProcNfo.UseShellExecute = false;
                            scProcNfo.WindowStyle = ProcessWindowStyle.Hidden;
                            scProcNfo.Arguments = "config \"FilterServiceProvider\" start= auto";
                            Process.Start(scProcNfo).WaitForExit();
                        }
                        catch(Exception e)
                        {
                            LoggerUtil.RecursivelyLogException(m_logger, e);
                        }

                        try
                        {
                            if(m_config != null && m_config.BlockInternet)
                            {
                                // While we're here, let's disable the internet so that the user
                                // can't browse the web without us. Only do this of course if configured.
                                try
                                {
                                    WFPUtility.DisableInternet();
                                }
                                catch { }
                            }
                        }
                        catch(Exception e)
                        {
                            LoggerUtil.RecursivelyLogException(m_logger, e);
                        }
                    }
                    else
                    {
                        // Means that our user got a granted deactivation request, or installed but
                        // never activated.
                        m_logger.Info("Shutting down without safeguards.");
                    }

                    // Flag that clean shutdown was completed already.
                    m_cleanShutdownComplete = true;
                }
            }
        }
    }
}