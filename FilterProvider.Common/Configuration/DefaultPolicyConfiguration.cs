/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

//﻿#define LOCAL_POLICY_CONFIGURATION

using Filter.Platform.Common.Extensions;

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
using Filter.Platform.Common.Data.Models;
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
using System.Text.RegularExpressions;
using DotNet.Globbing;
using System.Security.Principal;

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
        private static IPathProvider s_paths;

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

            s_paths = PlatformTypes.New<IPathProvider>();

            configFilePath = Path.Combine(s_paths.ApplicationDataFolder, "cfg.json");
            listDataFilePath = Path.Combine(s_paths.ApplicationDataFolder, "a.dat");

            // Setup json serialization settings.
            s_configSerializerSettings = new JsonSerializerSettings();
            s_configSerializerSettings.NullValueHandling = NullValueHandling.Ignore;
        }

        public AppConfigModel Configuration { get; set; }

        public FilterDbCollection FilterCollection { get { return m_filterCollection; } }
        public BagOfTextTriggers TextTriggers { get { return m_textTriggers; } }

        /// <summary>
        /// Stores all, if any, applications that should be forced through the filter. 
        /// </summary>
        public HashSet<string> BlacklistedApplications { get; private set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Stores all, if any, applications that should be bypassed by the filter
        /// </summary>
        public HashSet<string> WhitelistedApplications { get; private set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public HashSet<Glob> BlacklistedApplicationGlobs { get; private set; } = new HashSet<Glob>();
        public HashSet<Glob> WhitelistedApplicationGlobs { get; private set; } = new HashSet<Glob>();

        public CategoryIndex CategoryIndex { get { return m_categoryIndex; } }

        public ConcurrentDictionary<string, MappedFilterListCategoryModel> GeneratedCategoriesMap { get { return m_generatedCategoriesMap; } }

        public TimeRestrictionModel[] TimeRestrictions { get; private set; }
        public bool AreAnyTimeRestrictionsEnabled { get; private set; }

        public event EventHandler OnConfigurationLoaded;
        
        private string getSHA1ForFilePath(string filePath, bool isEncrypted)
        {
            if(!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
            {
                return null;
            }

            Stream stream = null;
            try
            {
                using (var fs = File.OpenRead(filePath))
                {
                    if(isEncrypted)
                    {
                        stream = RulesetEncryption.DecryptionStream(fs);
                    }
                    else
                    {
                        stream = fs;
                    }

                    using (SHA1 sec = new SHA1CryptoServiceProvider())
                    {
                        byte[] bt = sec.ComputeHash(stream);
                        var lHash = BitConverter.ToString(bt).Replace("-", "");

                        return lHash.ToLower();
                    }
                }
            }
            catch(Exception ex)
            {
                m_logger.Warn($"Could not calculate SHA1 for {filePath}: {ex}");
                return null;
            }
            finally
            {
                if (isEncrypted) stream?.Dispose();
            }
        }

        private string getListFolder()
        {
            return Path.Combine(s_paths.ApplicationDataFolder, @"rules");
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

        private string getListFilePath(string relativePath)
        {
            return Path.Combine(getListFolder(), relativePath.Replace('/', '.'));
        }

        private string getListFilePath(FilteringPlainTextListModel listModel)
            => getListFilePath(listModel.RelativeListPath);

        Dictionary<string, bool?> lastFilterListResults = null;

        public bool? VerifyLists()
        {
            // Assemble list of SHA1 hashes for existing lists.
            Dictionary<string, string> hashes = new Dictionary<string, string>();

            if(Configuration == null)
            {
                return null;
            }

            foreach(var list in Configuration.ConfiguredLists)
            {
                string listFilePath = getListFilePath(list);
                hashes[list.RelativeListPath] = getSHA1ForFilePath(listFilePath, isEncrypted: true);
            }

            Dictionary<string, bool?> filterListResults = WebServiceUtil.Default.VerifyLists(hashes);
            lastFilterListResults = filterListResults;

            if(filterListResults == null)
            {
                return null;
            }

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
                m_ipcServer?.NotifyStatus(FilterStatus.Synchronized);

                var rHash = Encoding.UTF8.GetString(rHashBytes);

                bool needsUpdate = false;
                string filePathSHA1 = getSHA1ForFilePath(configFilePath, isEncrypted: false);

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
                File.WriteAllBytes(configFilePath, configBytes);
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

            if(Configuration == null)
            {
                return null;
            }

            createListFolderIfNotExists();

            m_logger.Info("Updating filtering rules, rules missing or integrity violation.");

            List<FilteringPlainTextListModel> listsToFetch = new List<FilteringPlainTextListModel>();
            foreach(var list in Configuration.ConfiguredLists)
            {
                bool? listIsCurrent = false;

                if(lastFilterListResults != null && lastFilterListResults.TryGetValue(list.RelativeListPath, out listIsCurrent))
                {
                    if(listIsCurrent == false || listIsCurrent == null) { listsToFetch.Add(list); }
                }
            }

            bool responseReceived;
            byte[] listBytes = WebServiceUtil.Default.GetFilterLists(listsToFetch, out code, out responseReceived);

            if (listBytes != null)
            {
                Dictionary<string, string> rulesets = new Dictionary<string, string>();

                using (MemoryStream ms = new MemoryStream(listBytes))
                using (StreamReader reader = new StreamReader(ms))
                {
                    string currentList = null;
                    bool errorList = false;

                    StringBuilder fileBuilder = new StringBuilder();

                    string line = null;
                    while((line = reader.ReadLine()) != null)
                    {
                        if(string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        if(line.Contains("--startlist"))
                        {
                            currentList = line.Substring("--startlist".Length).TrimStart();
                        }
                        else if(line.StartsWith("--endlist"))
                        {
                            if(errorList)
                            {
                                errorList = false;
                            }
                            else
                            {
                                rulesets[currentList] = fileBuilder.ToString();
                                fileBuilder.Clear();
                                
                                try
                                {
                                    byte[] fileBytes = Encoding.UTF8.GetBytes(rulesets[currentList]);
                                    using (FileStream stream = new FileStream(getListFilePath(currentList), FileMode.Create))
                                    using (CryptoStream cs = RulesetEncryption.EncryptionStream(stream))
                                    {
                                        cs.Write(fileBytes, 0, fileBytes.Length);

                                        cs.FlushFinalBlock();
                                    }
                                }
                                catch(Exception ex)
                                {
                                    m_logger.Error($"Failed to write to rule path {getListFilePath(currentList)} {ex}");
                                }
                            }
                        }
                        else
                        {
                            if(line == "http-result 404")
                            {
                                m_logger.Error($"404 Error was returned for category {currentList}");
                                errorList = true;
                                continue;
                            }
                            fileBuilder.AppendLine(line);
                        }
                    }
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
                                            using (var encryptedStream = File.OpenRead(rulesetPath))
                                            {
                                                try
                                                {
                                                    using (var listStream = RulesetEncryption.DecryptionStream(encryptedStream))
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
                                                catch (Exception ex)
                                                {
                                                    m_logger.Info("CRIPPLED: {0}", ex);
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
                                            using (var encryptedStream = File.OpenRead(rulesetPath))
                                            using (var listStream = RulesetEncryption.DecryptionStream(encryptedStream))
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
                                            using (var encryptedStream = File.OpenRead(rulesetPath))
                                            using (var listStream = RulesetEncryption.DecryptionStream(encryptedStream))
                                            {
                                                try
                                                {
                                                    var triggersLoaded = m_textTriggers.LoadStoreFromStream(listStream, categoryModel.CategoryId).Result;

                                                    totalTriggersLoaded += (uint)triggersLoaded;

                                                    if (triggersLoaded > 0)
                                                    {
                                                        m_categoryIndex.SetIsCategoryEnabled(categoryModel.CategoryId, true);
                                                    }
                                                }
                                                catch(Exception ex)
                                                {
                                                    m_logger.Info("Error on LoadStoresFromStream {0}", ex);
                                                }
                                            }
                                        }

                                        GC.Collect();
                                    }
                                    break;

                                case PlainTextFilteringListType.Whitelist:
                                    {
                                        using (var encryptedStream = File.OpenRead(rulesetPath))
                                        using (var listStream = RulesetEncryption.DecryptionStream(encryptedStream))
                                        using (TextReader tr = new StreamReader(listStream))
                                        {
                                            if (TryFetchOrCreateCategoryMap(thisListCategoryName, out categoryModel))
                                            {
                                                var whitelistRules = new List<string>();
                                                string line = null;
                                                while ((line = tr.ReadLine()) != null)
                                                {
                                                    whitelistRules.Add("@@" + line.Trim() + "\n");
                                                }

                                                var loadedFailedRes = m_filterCollection.ParseStoreRules(whitelistRules.ToArray(), categoryModel.CategoryId).Result;
                                                totalFilterRulesLoaded += (uint)loadedFailedRes.Item1;
                                                totalFilterRulesFailed += (uint)loadedFailedRes.Item2;

                                                if (loadedFailedRes.Item1 > 0)
                                                {
                                                    m_categoryIndex.SetIsCategoryEnabled(categoryModel.CategoryId, true);
                                                }
                                            }
                                        }

                                        GC.Collect();
                                    }
                                    break;
                            }
                        }
                    }

                    if(Configuration != null && Configuration.CustomTriggerBlacklist != null && Configuration.CustomTriggerBlacklist.Count > 0)
                    {
                        MappedFilterListCategoryModel categoryModel = null;

                        // Always load triggers as blacklists.
                        if(TryFetchOrCreateCategoryMap("/user/trigger_blacklist", out categoryModel))
                        {
                            var triggersLoaded = m_textTriggers.LoadStoreFromList(Configuration.CustomTriggerBlacklist, categoryModel.CategoryId).Result;

                            totalTriggersLoaded += (uint)triggersLoaded;

                            if (triggersLoaded > 0)
                            {
                                m_categoryIndex.SetIsCategoryEnabled(categoryModel.CategoryId, true);
                            }

                            m_logger.Info("Number of triggers loaded for CustomTriggerBlacklist {0}", triggersLoaded);
                        }
                    }

                    if(Configuration != null && Configuration.CustomWhitelist != null && Configuration.CustomWhitelist.Count > 0)
                    {
                        List<string> sanitizedCustomWhitelist = new List<string>();

                        // As we are importing directly into an Adblock Plus-style rule engine, we need to make sure
                        // that the user can't whitelist sites by adding something with a "@@" in front of it.

                        // The easiest way to do this is to limit the characters to 'safe' characters.
                        Regex isCleanRule = new Regex(@"^[a-zA-Z0-9\-_\:\.\/]+$", RegexOptions.Compiled);
                        foreach(string site in Configuration.CustomWhitelist)
                        {
                            if(isCleanRule.IsMatch(site))
                            {
                                sanitizedCustomWhitelist.Add("@@" + site);
                            }
                        }

                        MappedFilterListCategoryModel categoryModel = null;
                        if(TryFetchOrCreateCategoryMap("/user/custom_whitelist", out categoryModel))
                        {
                            var loadedFailedRes = m_filterCollection.ParseStoreRules(sanitizedCustomWhitelist.ToArray(), categoryModel.CategoryId).Result;
                            totalFilterRulesLoaded += (uint)loadedFailedRes.Item1;
                            totalFilterRulesFailed += (uint)loadedFailedRes.Item2;

                            if (loadedFailedRes.Item1 > 0)
                            {
                                m_categoryIndex.SetIsCategoryEnabled(categoryModel.CategoryId, true);
                            }
                        }
                    }

                    if(Configuration != null && Configuration.SelfModeration != null && Configuration.SelfModeration.Count > 0)
                    {
                        List<string> sanitizedSelfModerationSites = new List<string>();

                        // As we are importing directly into an Adblock Plus-style rule engine, we need to make sure
                        // that the user can't whitelist sites by adding something with a "@@" in front of it.

                        // The easiest way to do this is to limit the characters to 'safe' characters.
                        Regex isCleanRule = new Regex(@"^[a-zA-Z0-9\-_\:\.\/]+$", RegexOptions.Compiled);

                        foreach(string site in Configuration.SelfModeration)
                        {
                            if(isCleanRule.IsMatch(site))
                            {
                                sanitizedSelfModerationSites.Add(site);
                            }
                        }

                        MappedFilterListCategoryModel categoryModel = null;
                        if (TryFetchOrCreateCategoryMap("/user/self_moderation", out categoryModel))
                        {
                            var loadedFailedRes = m_filterCollection.ParseStoreRules(sanitizedSelfModerationSites.ToArray(), categoryModel.CategoryId).Result;
                            totalFilterRulesLoaded += (uint)loadedFailedRes.Item1;
                            totalFilterRulesFailed += (uint)loadedFailedRes.Item2;

                            if(loadedFailedRes.Item1 > 0)
                            {
                                m_categoryIndex.SetIsCategoryEnabled(categoryModel.CategoryId, true);
                            }
                        }
                    }

                    m_filterCollection.FinalizeForRead();
                    m_filterCollection.InitializeBloomFilters();

                    m_textTriggers.FinalizeForRead();
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

        public bool LoadConfiguration()
        {
            try
            {
                m_filteringRwLock.EnterWriteLock();

                if(File.Exists(configFilePath))
                {
                    using (var cfgStream = File.OpenRead(configFilePath))
                    {
                        return LoadConfiguration(cfgStream);
                    }
                }

                m_logger.Error("Configuration file does not exist.");
                return false;
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
        }

        /// <summary>
        /// Queries the service provider for updated filtering rules. 
        /// </summary>
        /// <param name="cfgStream">Added this parameter so that we could test the default policy configuration.</param>
        public bool LoadConfiguration(Stream cfgStream)
        {
            try
            {
                // Load our configuration file and load configured lists, etc.
                if(cfgStream != null)
                {
                    string cfgJson = string.Empty;

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

                    loadAppList(BlacklistedApplications, Configuration.BlacklistedApplications, BlacklistedApplicationGlobs);
                    loadAppList(WhitelistedApplications, Configuration.WhitelistedApplications, WhitelistedApplicationGlobs);

                    TimeRestrictions = new TimeRestrictionModel[7];
                    
                    for(int i = 0; i < 7; i++)
                    {
                        DayOfWeek day = (DayOfWeek)i;

                        string configDay = day.ToString().ToLowerInvariant();

                        TimeRestrictionModel restriction = null;

                        Configuration.TimeRestrictions?.TryGetValue(configDay, out restriction);

                        TimeRestrictions[i] = restriction;
                    }

                    AreAnyTimeRestrictionsEnabled = TimeRestrictions.Any(r => r?.RestrictionsEnabled == true);

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

            return true;
        }

        private void loadAppList(HashSet<string> myAppList, HashSet<string> appList, HashSet<Glob> globs)
        {
            myAppList.Clear();

            globs?.Clear();

            foreach (var app in appList)
            {
                myAppList.Add(app);

                if(globs != null && (app.Contains('*') || app.Contains('?')))
                {
                    string globString = app;

                    try
                    {
                        // Check to see if the glob includes a ** at the beginning. This is required in order to match any partial paths.
                        if(globString[0] != '*' || globString[1] != '*')
                        {
                            globString = Path.Combine("**", globString);
                        }

                        var glob = Glob.Parse(globString, new GlobOptions()
                        {
                            Evaluation = new EvaluationOptions()
                            {
                                CaseInsensitive = true
                            }
                        });

                        if (glob != null)
                            globs.Add(glob);
                    }
                    catch (Exception ex)
                    {
                        m_logger.Warn("Invalid glob '{0}'. Not adding.", app);
                    }
                }
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
