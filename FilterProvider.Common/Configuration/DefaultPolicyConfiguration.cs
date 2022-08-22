/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

//﻿#define LOCAL_POLICY_CONFIGURATION

using Filter.Platform.Common.Extensions;

using CloudVeil.IPC.Messages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Filter.Platform.Common.Data.Models;
using System.Diagnostics;
using System.Threading;
using Newtonsoft.Json;
using System.Collections.Concurrent;

using Filter.Platform.Common.Util;
using FilterProvider.Common.Data.Filtering;
using FilterProvider.Common.Util;
using Filter.Platform.Common;
using System.Text.RegularExpressions;
using DotNet.Globbing;
using GoProxyWrapper;
using CloudVeil.IPC;

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
        private static IPathProvider paths;

        private static JsonSerializerSettings configSerializerSettings;

        public DefaultPolicyConfiguration(IPCServer server, NLog.Logger logger)
        {
            ipcServer = server;
            this.logger = logger;
            policyLock = new ReaderWriterLockSlim();
        }

        private IPCServer ipcServer;

        // Not sure yet whether this will be provided by WindowsPlatformServices or a common service provider.
        private NLog.Logger logger;

        // Need to consolidate global stuff some how.
        private ReaderWriterLockSlim policyLock;

        public ReaderWriterLockSlim PolicyLock => policyLock;

        //private FilterDbCollection m_filterCollection;

        private BagOfTextTriggers textTriggers;

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

            paths = PlatformTypes.New<IPathProvider>();

            configFilePath = Path.Combine(paths.ApplicationDataFolder, "cfg.json");
            listDataFilePath = Path.Combine(paths.ApplicationDataFolder, "a.dat");

            // Setup json serialization settings.
            configSerializerSettings = new JsonSerializerSettings();
            configSerializerSettings.NullValueHandling = NullValueHandling.Ignore;
        }

        public AppConfigModel Configuration { get; set; }

        //public FilterDbCollection FilterCollection { get { return m_filterCollection; } }
        public BagOfTextTriggers TextTriggers { get { return textTriggers; } }

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

        public event EventHandler ListsReloaded;

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
                logger.Warn($"Could not calculate SHA1 for {filePath}: {ex}");
                return null;
            }
            finally
            {
                try
                {
                    if (isEncrypted) stream?.Dispose();
                }
                catch(Exception ex)
                {
                    logger.Warn("Error occurred while disposing stream: {0}", ex);
                }
            }
        }

        private string getTempFolder() => paths.GetPath("temp");

        private string getListFolder() => paths.GetPath("rules");

        private void createListFolderIfNotExists()
        {
            string listFolder = getListFolder();

            if(!Directory.Exists(listFolder))
            {
                DirectoryInfo dirInfo = Directory.CreateDirectory(listFolder);
                dirInfo.Attributes = FileAttributes.Directory | FileAttributes.Hidden | FileAttributes.System;
            }
        }

        private string getListFilePath(string relativePath, string listFolder = null)
        {
            return Path.Combine(listFolder ?? getListFolder(), relativePath.Replace('/', '.'));
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
                ipcServer?.NotifyStatus(FilterStatus.Synchronized);

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

            logger.Info("Updating filtering rules, rules missing or integrity violation.");
            var configBytes = WebServiceUtil.Default.RequestResource(ServiceResource.UserConfigRequest, out code);

            if (code == HttpStatusCode.OK && configBytes != null && configBytes.Length > 0)
            {
                File.WriteAllBytes(configFilePath, configBytes);
                return true;
            }
            else
            {
                Debug.WriteLine("Failed to download configuration data.");
                logger.Error("Failed to download configuration data.");
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

            logger.Info("Updating filtering rules, rules missing or integrity violation.");

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
                                    logger.Error($"Failed to write to rule path {getListFilePath(currentList)} {ex}");
                                }
                            }
                        }
                        else
                        {
                            if(line == "http-result 404")
                            {
                                logger.Error($"404 Error was returned for category {currentList}");
                                errorList = true;
                                continue;
                            }
                            fileBuilder.Append($"{line}\n");
                        }
                    }
                }
            }

            return true;
        }

        private bool decryptLists(string listFolderPath, string tempFolderPath)
        {
            if(!Directory.Exists(tempFolderPath))
            {
                Directory.CreateDirectory(tempFolderPath);
            }

            if(Configuration == null)
            {
                return false;
            }

            foreach(var listModel in Configuration.ConfiguredLists)
            {
                string path = getListFilePath(listModel.RelativeListPath, listFolderPath);

                try
                {
                    string tempPath = Path.Combine(tempFolderPath, Path.GetFileName(path));

                    using (var encryptedStream = File.OpenRead(path))
                    using (var cs = RulesetEncryption.DecryptionStream(encryptedStream))
                    using (var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                    {
                        cs.CopyTo(output);
                    }
                }
                catch(Exception ex)
                {
                    logger.Error($"decryptLists threw exception for {path}: {ex}");
                //    return false;
                }
            }

            return true;
        }

        private void deleteTemporaryLists()
        {
            try
            {
                foreach(string filePath in Directory.EnumerateFiles(getTempFolder()))
                {
                    File.Delete(filePath);
                }

                Directory.Delete(getTempFolder());
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to delete temporary ruleset folder. {ex}");
            }
        }

        public bool LoadLists()
        {
            try
            {
                policyLock.EnterWriteLock();

                var listFolderPath = getListFolder();

                if (Directory.Exists(listFolderPath))
                {
                    // Recreate our filter collection and reset all categories to be disabled.
                    AdBlockMatcherApi.Initialize();

                    // Recreate our triggers container.
                    if (textTriggers != null)
                    {
                        textTriggers.Dispose();
                    }

                    m_categoryIndex.SetAll(false);

                    // XXX TODO - Maybe make it a compiler flag to toggle if this is going to
                    // be an in-memory DB or not.
                    textTriggers = new BagOfTextTriggers(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "t.dat"), true, true, logger);

                    // Now clear all generated categories. These will be re-generated as needed.
                    m_generatedCategoriesMap.Clear();

                    uint totalFilterRulesLoaded = 0;
                    uint totalFilterRulesFailed = 0;
                    uint totalTriggersLoaded = 0;

                    // Load all configured list files.
                    string tempFolder = getTempFolder();

                    decryptLists(getListFolder(), tempFolder);

                    var rulePath = paths.GetPath("rules.dat");

                    if (File.Exists(rulePath))
                    {
                      //  AdBlockMatcherApi.Load(rulePath);
                    }
                    foreach (var listModel in Configuration.ConfiguredLists)
                    {
                        var rulesetPath = getListFilePath(listModel.RelativeListPath, tempFolder);

                        if(File.Exists(rulesetPath))
                        {
                            var thisListCategoryName = listModel.RelativeListPath.Substring(0, listModel.RelativeListPath.LastIndexOfAny(new[] { '/', '\\' }) + 1) + Path.GetFileNameWithoutExtension(listModel.RelativeListPath);

                            MappedFilterListCategoryModel categoryModel = null;

                            switch (listModel.ListType)
                            {
                                case PlainTextFilteringListType.Blacklist:
                                    {
                                        if (TryFetchOrCreateCategoryMap(thisListCategoryName, listModel.ListType, out categoryModel))
                                        {
                                            AdBlockMatcherApi.ParseRuleFile(rulesetPath, categoryModel.CategoryId, ListType.Blacklist);
                                            m_categoryIndex.SetIsCategoryEnabled(categoryModel.CategoryId, true);
                                        }
                                    }
                                    break;

                                case PlainTextFilteringListType.BypassList:
                                    {
                                        MappedBypassListCategoryModel bypassCategoryModel = null;

                                        // Must be loaded twice. Once as a blacklist, once as a whitelist.
                                        if (TryFetchOrCreateCategoryMap(thisListCategoryName, listModel.ListType, out bypassCategoryModel))
                                        {
                                            AdBlockMatcherApi.ParseRuleFile(rulesetPath, bypassCategoryModel.CategoryId, ListType.BypassList);
                                            m_categoryIndex.SetIsCategoryEnabled(bypassCategoryModel.CategoryId, true);
                                            GC.Collect();
                                        }
                                    }
                                    break;

                                case PlainTextFilteringListType.TextTrigger:
                                    {
                                        // Always load triggers as blacklists.
                                        if (TryFetchOrCreateCategoryMap(thisListCategoryName, listModel.ListType, out categoryModel))
                                        {
                                            using (var listStream = File.OpenRead(rulesetPath))
                                            {
                                                try
                                                {
                                                    var triggersLoaded = textTriggers.LoadStoreFromStream(listStream, categoryModel.CategoryId).Result;

                                                    totalTriggersLoaded += (uint)triggersLoaded;

                                                    if (triggersLoaded > 0)
                                                    {
                                                        m_categoryIndex.SetIsCategoryEnabled(categoryModel.CategoryId, true);
                                                    }
                                                }
                                                catch(Exception ex)
                                                {
                                                    logger.Info($"Error on LoadStoresFromStream {ex}");
                                                }
                                            }
                                        }

                                        GC.Collect();
                                    }
                                    break;

                                case PlainTextFilteringListType.Whitelist:
                                    {
                                        if(TryFetchOrCreateCategoryMap(thisListCategoryName, listModel.ListType, out categoryModel))
                                        {
                                            AdBlockMatcherApi.ParseRuleFile(rulesetPath, categoryModel.CategoryId, ListType.Whitelist);
                                            m_categoryIndex.SetIsCategoryEnabled(categoryModel.CategoryId, true);
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
                        if(TryFetchOrCreateCategoryMap("/user/trigger_blacklist", PlainTextFilteringListType.TextTrigger, out categoryModel))
                        {
                            var triggersLoaded = textTriggers.LoadStoreFromList(Configuration.CustomTriggerBlacklist, categoryModel.CategoryId).Result;

                            totalTriggersLoaded += (uint)triggersLoaded;

                            if (triggersLoaded > 0)
                            {
                                m_categoryIndex.SetIsCategoryEnabled(categoryModel.CategoryId, true);
                            }

                            logger.Info("Number of triggers loaded for CustomTriggerBlacklist {0}", triggersLoaded);
                        }
                    }

                    if(Configuration != null && Configuration.CustomWhitelist != null && Configuration.CustomWhitelist.Count > 0)
                    {
                        AddCustomConfiguredSiteList(Configuration.CustomWhitelist, tempFolder, ".user.custom_whitelist.rules.txt", "/user/custom_whitelist", PlainTextFilteringListType.Whitelist, ListType.Whitelist);
                    }

                    if (Configuration != null && Configuration.CustomBypasslist != null && Configuration.CustomBypasslist.Count > 0)
                    {
                        AddCustomConfiguredSiteList(Configuration.CustomBypasslist, tempFolder, ".user.custom_bypasslist.rules.txt", "/user/custom_bypasslist", PlainTextFilteringListType.BypassList, ListType.BypassList);
                    }

                    if (Configuration != null && Configuration.SelfModeration != null && Configuration.SelfModeration.Count > 0)
                    {
                        AddCustomConfiguredSiteList(Configuration.SelfModeration, tempFolder, ".user.self_moderation.rules.txt", "/user/self_moderation", PlainTextFilteringListType.Blacklist, ListType.Blacklist);
                    }

                    //m_filterCollection.FinalizeForRead();
                    //m_filterCollection.InitializeBloomFilters();

                    textTriggers.FinalizeForRead();
                    textTriggers.InitializeBloomFilters();

               //     AdBlockMatcherApi.Save(s_paths.GetPath("rules.dat"));

                    ListsReloaded?.Invoke(this, new EventArgs());

                    logger.Info("Loaded {0} rules, {1} rules failed most likely due to being malformed, and {2} text triggers loaded.", totalFilterRulesLoaded, totalFilterRulesFailed, totalTriggersLoaded);
                }
                AdBlockMatcherApi.LoadingFinished();

                return true;
            }
            catch(Exception ex)
            {
                LoggerUtil.RecursivelyLogException(logger, ex);
                return false;
            }
            finally
            {
                policyLock.ExitWriteLock();

                deleteTemporaryLists();
            }
        }

        private void AddCustomConfiguredSiteList(List<string> ruleSet, string tempFolder, string fileName, string categoryPath, PlainTextFilteringListType plainTextFilteringListType, ListType mappedListType)
        {
            // As we are importing directly into an Adblock Plus-style rule engine, we need to make sure
            // that the user can't whitelist sites by adding something with a "@@" in front of it.

            // The easiest way to do this is to limit the characters to 'safe' characters.
            Regex isCleanRule = new Regex(@"^[a-zA-Z0-9\-_\:\.\/]+$", RegexOptions.Compiled);
            string rulesetPath = Path.Combine(tempFolder, fileName);

            using (var rulesetStream = File.OpenWrite(rulesetPath))
            using (var writer = new StreamWriter(rulesetStream))
            {
                foreach (string site in ruleSet)
                {
                    if (site == null) continue;

                    if (isCleanRule.IsMatch(site))
                    {
                        writer.WriteLine($"||{site}");
                    }
                }
            }

            if (mappedListType == ListType.BypassList)
            {
                MappedBypassListCategoryModel categoryModel = null;
                if (TryFetchOrCreateCategoryMap(categoryPath, plainTextFilteringListType, out categoryModel))
                {
                    AdBlockMatcherApi.ParseRuleFile(rulesetPath, categoryModel.CategoryId, mappedListType);
                    m_categoryIndex.SetIsCategoryEnabled(categoryModel.CategoryId, true);
                }
            }
            else
            {
                MappedFilterListCategoryModel categoryModel = null;
                if (TryFetchOrCreateCategoryMap(categoryPath, plainTextFilteringListType, out categoryModel))
                {
                    AdBlockMatcherApi.ParseRuleFile(rulesetPath, categoryModel.CategoryId, mappedListType);
                    m_categoryIndex.SetIsCategoryEnabled(categoryModel.CategoryId, true);
                }
            }
        }

        public bool LoadConfiguration()
        {
            try
            {
                policyLock.EnterWriteLock();

                if(File.Exists(configFilePath))
                {
                    using (var cfgStream = File.OpenRead(configFilePath))
                    {
                        return LoadConfiguration(cfgStream);
                    }
                }

                logger.Error("Configuration file does not exist.");
                return false;          
            }
            catch (Exception e)
            {
                LoggerUtil.RecursivelyLogException(logger, e);
                return false;
            }
            finally
            {
                policyLock.ExitWriteLock();
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
                        logger.Error("Could not find valid JSON config for filter.");
                        return false;
                    }

                    try
                    {
                        LoadConfigFromJson(cfgJson, configSerializerSettings);
                        logger.Info("Configuration loaded from JSON.");
                    }
                    catch(Exception deserializationError)
                    {
                        logger.Error("Failed to deserialize JSON config.");
                        LoggerUtil.RecursivelyLogException(logger, deserializationError);
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
                LoggerUtil.RecursivelyLogException(logger, e);
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
                    catch (Exception)
                    {
                        logger.Warn("Invalid glob '{0}'. Not adding.", app);
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
        private bool TryFetchOrCreateCategoryMap<T>(string categoryName, PlainTextFilteringListType listType, out T model) where T : MappedFilterListCategoryModel
        {
            logger.Info("CATEGORY {0}", categoryName);

            MappedFilterListCategoryModel existingCategory = null;
            if (!m_generatedCategoriesMap.TryGetValue(categoryName, out existingCategory))
            {
                // We can't generate anymore categories. Sorry, but the rest get ignored.
                if (m_generatedCategoriesMap.Count >= short.MaxValue)
                {
                    logger.Error("The maximum number of filtering categories has been exceeded.");
                    model = null;
                    return false;
                }

                if (typeof(T) == typeof(MappedBypassListCategoryModel))
                {
                    MappedFilterListCategoryModel secondCategory = null;

                    if (TryFetchOrCreateCategoryMap(categoryName + "_as_whitelist", PlainTextFilteringListType.Whitelist, out secondCategory))
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
                    var newModel = (T)new MappedFilterListCategoryModel((byte)((m_generatedCategoriesMap.Count) + 1), categoryName, listType);
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
