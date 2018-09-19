/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

//﻿#define LOCAL_POLICY_CONFIGURATION

using Citadel.Core.Windows.Types;
using Citadel.Core.Windows.Util;
using Citadel.Core.Extensions;

using Citadel.IPC;
using Citadel.IPC.Messages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FilterProvider.Common.Data.Models;
using System.Diagnostics;
using System.Threading;
using System.IO.Compression;
using Newtonsoft.Json;
using DistillNET;
using Te.Citadel.Util;
using System.Collections.Concurrent;
using System.Security.AccessControl;

using Filter.Platform.Common.Util;
using FilterProvider.Common.Data.Filtering;
using FilterProvider.Common.Util;
using Filter.Platform.Common;

namespace FilterProvider.Common.Configuration
{
    /*
     * Documenting our data file changes:
     * There are now two calls that we expect on the API side.
     * /my/config - this gets the configuration JSON for the currently authenticated user.
     * /my/rules - this gets all the rules available for the filter as a zipped file.
     * 
     * The zipped file folder structure is:
     * / - this is root, all rule folders are in root.
     * /adult_rules - this is a rule file container
     * /adult_rules/rules.txt - this is the actual meat of the rule data.
     */

    public class DefaultPolicyConfiguration : IPolicyConfiguration
    {
#if LOCAL_POLICY_CONFIGURATION
        private static string serverConfigFilePath;
        private static string serverListDataFilePath;
#endif

        private static string configFilePath;
        private static string listDataFilePath;

        private static JsonSerializerSettings s_configSerializerSettings;

        public DefaultPolicyConfiguration(IPCServer server, NLog.Logger logger, ReaderWriterLockSlim filteringLock)
        {
            m_ipcServer = server;
            m_logger = logger;
            m_filteringRwLock = filteringLock;
        }

        // FIXME: This does not belong in CitadelService.Common. Use an interface instead for implementing this.
        // IPlatformServices implemented by WindowsPlatformServices
        private IPCServer m_ipcServer;

        // Not sure yet whether this will be provided by WindowsPlatformServices or a common service provider.
        private NLog.Logger m_logger;

        // Need to consolidate global stuff some how.
        private ReaderWriterLockSlim m_filteringRwLock;

        private FilterDbCollection m_filterCollection;

        private BagOfTextTriggers m_textTriggers;

        /// <summary>
        /// Stores all, if any, applications that should be forced throught the filter. 
        /// </summary>
        private HashSet<string> m_blacklistedApplications = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Stores all, if any, applications that should not be forced through the filter. 
        /// </summary>
        private HashSet<string> m_whitelistedApplications = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Whenever we load filtering rules, we simply make up numbers for categories as we go
        /// along. We use this object to store what strings we map to numbers.
        /// </summary>
        private ConcurrentDictionary<string, MappedFilterListCategoryModel> m_generatedCategoriesMap = new ConcurrentDictionary<string, MappedFilterListCategoryModel>(StringComparer.OrdinalIgnoreCase);

        private CategoryIndex m_categoryIndex = new CategoryIndex(short.MaxValue);

        static DefaultPolicyConfiguration()
        {

#if LOCAL_POLICY_CONFIGURATION
            serverConfigFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CloudVeil", "server-cfg.json");
            serverListDataFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CloudVeil", "server-a.dat");
#endif

            // cfg.json and data path? TODO FIXME
            configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cfg.json");
            listDataFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "a.dat");

            // Setup json serialization settings.
            s_configSerializerSettings = new JsonSerializerSettings();
            s_configSerializerSettings.NullValueHandling = NullValueHandling.Ignore;
        }

        public AppConfigModel Configuration { get; set; }

        public FilterDbCollection FilterCollection { get { return m_filterCollection; } }
        public BagOfTextTriggers TextTriggers { get { return m_textTriggers; } }

        public HashSet<string> BlacklistedApplications { get { return m_blacklistedApplications; } }
        public HashSet<string> WhitelistedApplications { get { return m_whitelistedApplications; } }

        public CategoryIndex CategoryIndex { get { return m_categoryIndex; } }

        public ConcurrentDictionary<string, MappedFilterListCategoryModel> GeneratedCategoriesMap { get { return m_generatedCategoriesMap; } }

        public event EventHandler OnConfigurationLoaded;
        
        private string getSHA1ForFilePath(string filePath)
        {
            if(!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
            {
                return null;
            }

            using (var fs = File.OpenRead(filePath))
            {
                using (SHA1 sec = new SHA1CryptoServiceProvider())
                {
                    byte[] bt = sec.ComputeHash(fs);
                    var lHash = BitConverter.ToString(bt).Replace("-", "");

                    return lHash.ToLower();
                }
            }
        }

        private string getListFolder()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"CloudVeil", @"rules");
        }

        private void createListFolderIfNotExists()
        {
            string listFolder = getListFolder();

            if(!Directory.Exists(listFolder))
            {
                DirectoryInfo dirInfo = Directory.CreateDirectory(listFolder);
                dirInfo.Attributes = FileAttributes.Directory | FileAttributes.Hidden | FileAttributes.System;
            }
        }

        private string getListFilePath(FilteringPlainTextListModel listModel)
        {
            return Path.Combine(getListFolder(), listModel.RelativeListPath.Replace('/', '.'));
        }

        Dictionary<string, bool?> lastFilterListResults = null;

        public bool? VerifyLists()
        {
            // Assemble list of SHA1 hashes for existing lists.
            Dictionary<string, string> hashes = new Dictionary<string, string>();

            foreach(var list in Configuration.ConfiguredLists)
            {
                string listFilePath = getListFilePath(list);
                hashes[list.RelativeListPath] = getSHA1ForFilePath(listFilePath);
            }

            Dictionary<string, bool?> filterListResults = WebServiceUtil.Default.VerifyLists(hashes);
            lastFilterListResults = filterListResults;

            foreach (var result in filterListResults)
            {
                if (result.Value == null)
                {
                    return null;
                }
                else if (result.Value == false)
                {
                    return false;
                }
            }

            return true;
        }

        public bool? VerifyConfiguration()
        {
            HttpStatusCode code;
            var rHashBytes = WebServiceUtil.Default.RequestResource(ServiceResource.UserConfigSumCheck, out code);

            if (code == HttpStatusCode.OK && rHashBytes != null)
            {
                // Notify all clients that we just successfully made contact with the server.
                // We don't set the status here, because we'd have to store it and set it
                // back, so we just directly issue this msg.
                m_ipcServer.NotifyStatus(FilterStatus.Synchronized);

                var rHash = Encoding.UTF8.GetString(rHashBytes);

                bool needsUpdate = false;
                string filePathSHA1 = getSHA1ForFilePath(configFilePath);

                if (filePathSHA1 == null)
                {
                    needsUpdate = true;
                }
                else
                {
                    if (!filePathSHA1.OIEquals(rHash))
                    {
                        needsUpdate = true;
                    }
                }

                return !needsUpdate;
            }
            else
            {
                return null;
            }
        }

        public bool? DownloadConfiguration()
        {
#if LOCAL_POLICY_CONFIGURATION
            if(VerifyConfiguration() == true)
            {
                return false;
            }

            var configBytes = File.ReadAllBytes(serverConfigFilePath);

            File.WriteAllBytes(configFilePath, configBytes);
            return true;
#else
            HttpStatusCode code;

            bool? isVerified = VerifyConfiguration();
            if (isVerified == true)
            {
                return false;
            }

            m_logger.Info("Updating filtering rules, rules missing or integrity violation.");
            var configBytes = WebServiceUtil.Default.RequestResource(ServiceResource.UserConfigRequest, out code);

            if (code == HttpStatusCode.OK && configBytes != null && configBytes.Length > 0)
            {
                File.WriteAllBytes(listDataFilePath, configBytes);
                return true;
            }
            else
            {
                Debug.WriteLine("Failed to download configuration data.");
                m_logger.Error("Failed to download configuration data.");
                return null;
            }
#endif
        }

        /// <summary>
        /// Helper function that calls OnConfigLoaded event after configuration is loaded.
        /// </summary>
        /// <param name="json"></param>
        /// <param name="settings"></param>
        private void LoadConfigFromJson(string json, JsonSerializerSettings settings)
        {
            Configuration = JsonConvert.DeserializeObject<AppConfigModel>(json, settings);
            OnConfigurationLoaded?.Invoke(this, new EventArgs());
        }

        public bool? DownloadLists()
        {
            HttpStatusCode code;

            bool? isVerified = VerifyLists();
            if (isVerified == true)
            {
                return false;
            }

            createListFolderIfNotExists();

            m_logger.Info("Updating filtering rules, rules missing or integrity violation.");

            foreach(var list in Configuration.ConfiguredLists)
            {
                bool? listIsCurrent = false;

                if(lastFilterListResults != null && lastFilterListResults.TryGetValue(list.RelativeListPath, out listIsCurrent))
                {
                    Console.WriteLine("{0} = {1}", list.RelativeListPath, listIsCurrent);

                    if(listIsCurrent == true)
                    {
                        continue;
                    }
                }

                string[] pathParts = list.RelativeListPath.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                string @namespace = pathParts[0];
                string category = pathParts[1];
                string typeName = Path.GetFileNameWithoutExtension(pathParts[2]);

                byte[] listBytes = WebServiceUtil.Default.GetFilterList(@namespace, category, typeName);

                if (listBytes != null)
                {
                    File.WriteAllBytes(getListFilePath(list), listBytes);
                }
                else
                {
                    return null;
                }
            }

            return true;
        }

        public bool LoadLists()
        {
            try
            {
                m_filteringRwLock.EnterWriteLock();

                var listFolderPath = getListFolder();

                if (Directory.Exists(listFolderPath))
                {
                    // Recreate our filter collection and reset all categories to be disabled.
                    if (m_filterCollection != null)
                    {
                        m_filterCollection.Dispose();
                    }

                    // Recreate our triggers container.
                    if (m_textTriggers != null)
                    {
                        m_textTriggers.Dispose();
                    }

                    m_filterCollection = new FilterDbCollection();

                    m_categoryIndex.SetAll(false);

                    // XXX TODO - Maybe make it a compiler flag to toggle if this is going to
                    // be an in-memory DB or not.
                    m_textTriggers = new BagOfTextTriggers(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "t.dat"), true, true, m_logger);

                    // Now clear all generated categories. These will be re-generated as needed.
                    m_generatedCategoriesMap.Clear();

                    uint totalFilterRulesLoaded = 0;
                    uint totalFilterRulesFailed = 0;
                    uint totalTriggersLoaded = 0;

                    // Load all configured list files.
                    foreach (var listModel in Configuration.ConfiguredLists)
                    {
                        var rulesetPath = getListFilePath(listModel);

                        if(File.Exists(rulesetPath))
                        {
                            var thisListCategoryName = listModel.RelativeListPath.Substring(0, listModel.RelativeListPath.LastIndexOfAny(new[] { '/', '\\' }) + 1) + Path.GetFileNameWithoutExtension(listModel.RelativeListPath);

                            MappedFilterListCategoryModel categoryModel = null;

                            switch (listModel.ListType)
                            {
                                case PlainTextFilteringListType.Blacklist:
                                    {
                                        if (TryFetchOrCreateCategoryMap(thisListCategoryName, out categoryModel))
                                        {
                                            using (var listStream = File.OpenRead(rulesetPath))
                                            {
                                                var loadedFailedRes = m_filterCollection.ParseStoreRulesFromStream(listStream, categoryModel.CategoryId).Result;
                                                totalFilterRulesLoaded += (uint)loadedFailedRes.Item1;
                                                totalFilterRulesFailed += (uint)loadedFailedRes.Item2;

                                                if (loadedFailedRes.Item1 > 0)
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
                                        if (TryFetchOrCreateCategoryMap(thisListCategoryName, out bypassCategoryModel))
                                        {
                                            // Load first as blacklist.
                                            using (var listStream = File.OpenRead(rulesetPath))
                                            {
                                                var loadedFailedRes = m_filterCollection.ParseStoreRulesFromStream(listStream, bypassCategoryModel.CategoryId).Result;
                                                totalFilterRulesLoaded += (uint)loadedFailedRes.Item1;
                                                totalFilterRulesFailed += (uint)loadedFailedRes.Item2;

                                                if (loadedFailedRes.Item1 > 0)
                                                {
                                                    m_categoryIndex.SetIsCategoryEnabled(bypassCategoryModel.CategoryId, true);
                                                }
                                            }

                                            GC.Collect();
                                        }
                                    }
                                    break;

                                case PlainTextFilteringListType.TextTrigger:
                                    {
                                        // Always load triggers as blacklists.
                                        if (TryFetchOrCreateCategoryMap(thisListCategoryName, out categoryModel))
                                        {
                                            using (var listStream = File.OpenRead(rulesetPath))
                                            {
                                                var triggersLoaded = m_textTriggers.LoadStoreFromStream(listStream, categoryModel.CategoryId).Result;
                                                m_textTriggers.FinalizeForRead();

                                                totalTriggersLoaded += (uint)triggersLoaded;

                                                if (triggersLoaded > 0)
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
                                        using (TextReader tr = new StreamReader(File.OpenRead(rulesetPath)))
                                        {
                                            if (TryFetchOrCreateCategoryMap(thisListCategoryName, out categoryModel))
                                            {
                                                var whitelistRules = new List<string>();
                                                string line = null;
                                                while ((line = tr.ReadLine()) != null)
                                                {
                                                    whitelistRules.Add("@@" + line.Trim() + "\n");
                                                }

                                                using (var listStream = File.OpenRead(rulesetPath))
                                                {
                                                    var loadedFailedRes = m_filterCollection.ParseStoreRules(whitelistRules.ToArray(), categoryModel.CategoryId).Result;
                                                    totalFilterRulesLoaded += (uint)loadedFailedRes.Item1;
                                                    totalFilterRulesFailed += (uint)loadedFailedRes.Item2;

                                                    if (loadedFailedRes.Item1 > 0)
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

                    m_filterCollection.FinalizeForRead();
                    m_filterCollection.InitializeBloomFilters();

                    m_textTriggers.InitializeBloomFilters();

                    m_logger.Info("Loaded {0} rules, {1} rules failed most likely due to being malformed, and {2} text triggers loaded.", totalFilterRulesLoaded, totalFilterRulesFailed, totalTriggersLoaded);
                }
                
                return true;
            }
            catch(Exception ex)
            {
                LoggerUtil.RecursivelyLogException(m_logger, ex);
                return false;
            }
            finally
            {
                m_filteringRwLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Queries the service provider for updated filtering rules. 
        /// </summary>
        public bool LoadConfiguration()
        {
            try
            {
                m_filteringRwLock.EnterWriteLock();

                // Load our configuration file and load configured lists, etc.

                if(File.Exists(configFilePath))
                {
                    string cfgJson = string.Empty;

                    using (var cfgStream = File.OpenRead(configFilePath))
                    using (TextReader textReader = new StreamReader(cfgStream))
                    {
                        cfgJson = textReader.ReadToEnd();
                    }

                    if(!StringExtensions.Valid(cfgJson))
                    {
                        m_logger.Error("Could not find valid JSON config for filter.");
                        return false;
                    }

                    try
                    {
                        LoadConfigFromJson(cfgJson, s_configSerializerSettings);
                        m_logger.Info("Configuration loaded from JSON.");
                    }
                    catch(Exception deserializationError)
                    {
                        m_logger.Error("Failed to deserialize JSON config.");
                        LoggerUtil.RecursivelyLogException(m_logger, deserializationError);
                        return false;
                    }

                    if (Configuration.UpdateFrequency.Minutes <= 0 || Configuration.UpdateFrequency == Timeout.InfiniteTimeSpan)
                    {
                        // Just to ensure that we enforce a minimum value here.
                        Configuration.UpdateFrequency = TimeSpan.FromMinutes(5);
                    }

                    loadAppList(m_blacklistedApplications, Configuration.BlacklistedApplications);
                    loadAppList(m_whitelistedApplications, Configuration.WhitelistedApplications);

                    if (Configuration.CannotTerminate)
                    {
                        // Turn on process protection if requested.
                        PlatformTypes.New<IAntitampering>().EnableProcessProtection();
                    }
                    else
                    {
                        PlatformTypes.New<IAntitampering>().DisableProcessProtection();
                    }

                    // Don't do list loading in the same function as configuration loading, because those are now indeed two separate functions.
                }
            }
            catch (Exception e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
                return false;
            }
            finally
            {
                m_filteringRwLock.ExitWriteLock();
            }

            return true;
        }

        private void loadAppList(HashSet<string> myAppList, HashSet<string> appList)
        {
            myAppList.Clear();
            foreach (var app in appList)
            {
                myAppList.Add(app);
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
            if (!m_generatedCategoriesMap.TryGetValue(categoryName, out existingCategory))
            {
                // We can't generate anymore categories. Sorry, but the rest get ignored.
                if (m_generatedCategoriesMap.Count >= short.MaxValue)
                {
                    m_logger.Error("The maximum number of filtering categories has been exceeded.");
                    model = null;
                    return false;
                }

                if (typeof(T) == typeof(MappedBypassListCategoryModel))
                {
                    MappedFilterListCategoryModel secondCategory = null;

                    if (TryFetchOrCreateCategoryMap(categoryName + "_as_whitelist", out secondCategory))
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
    }
}
