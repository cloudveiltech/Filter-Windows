/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using Citadel.Core.Windows.Types;
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
using FilterProvider.Common.Data.Filtering;
using Te.Citadel.Util;
using System.Collections.Concurrent;
using Filter.Platform.Common.Util;
using Filter.Platform.Common;
using FilterProvider.Common.Util;

namespace FilterProvider.Common.Configuration
{
    internal class OldPolicyConfiguration : IPolicyConfiguration
    {
        private static string listDataFilePath;
        private static JsonSerializerSettings s_configSerializerSettings;

        public OldPolicyConfiguration(IPCServer server, NLog.Logger logger, ReaderWriterLockSlim filteringLock)
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

        static OldPolicyConfiguration()
        {
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

        public bool? VerifyConfiguration()
        {
            HttpStatusCode code;
            var rHashBytes = WebServiceUtil.Default.RequestResource(ServiceResource.UserDataSumCheck, out code);

            if(code == HttpStatusCode.OK && rHashBytes != null)
            {
                // Notify all clients that we just successfully made contact with the server.
                // We don't set the status here, because we'd have to store it and set it
                // back, so we just directly issue this msg.
                m_ipcServer.NotifyStatus(FilterStatus.Synchronized);

                var rHash = Encoding.UTF8.GetString(rHashBytes);

                bool needsUpdate = false;

                if(!File.Exists(listDataFilePath) || new FileInfo(listDataFilePath).Length == 0)
                {
                    needsUpdate = true;
                }
                else
                {
                    // We're going to hash our local version and compare. If they don't match, we're
                    // going to update our lists.

                    using (var fs = File.OpenRead(listDataFilePath))
                    {
                        using (SHA1 sec = new SHA1CryptoServiceProvider())
                        {
                            byte[] bt = sec.ComputeHash(fs);
                            var lHash = BitConverter.ToString(bt).Replace("-", "");

                            if (!lHash.OIEquals(rHash))
                            {
                                needsUpdate = true;
                            }
                        }
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
            HttpStatusCode code;

            bool? isVerified = VerifyConfiguration();
            if(isVerified == true)
            {
                return false;
            }

            m_logger.Info("Updating filtering rules, rules missing or integrity violation.");
            var filterDataZipBytes = WebServiceUtil.Default.RequestResource(ServiceResource.UserDataRequest, out code);

            if (code == HttpStatusCode.OK && filterDataZipBytes != null && filterDataZipBytes.Length > 0)
            {
                File.WriteAllBytes(listDataFilePath, filterDataZipBytes);
                return true;
            }
            else
            {
                Debug.WriteLine("Failed to download list data.");
                m_logger.Error("Failed to download list data.");
                return null;
            }
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

        /// <summary>
        /// Queries the service provider for updated filtering rules. 
        /// </summary>
        public bool LoadConfiguration()
        {
            try
            {
                m_filteringRwLock.EnterWriteLock();

                // Load our configuration file and load configured lists, etc.
                var configDataFilePath = listDataFilePath;

                if (File.Exists(configDataFilePath))
                {
                    using (var file = File.OpenRead(configDataFilePath))
                    {
                        using (var zip = new ZipArchive(file, ZipArchiveMode.Read))
                        {
                            // Find the configuration JSON file.
                            string cfgJson = string.Empty;
                            foreach (var entry in zip.Entries)
                            {
                                if (entry.Name.OIEquals("cfg.json"))
                                {
                                    using (var cfgStream = entry.Open())
                                    using (TextReader tr = new StreamReader(cfgStream))
                                    {
                                        cfgJson = tr.ReadToEnd();
                                        break;
                                    }
                                }
                            }

                            if (!StringExtensions.Valid(cfgJson))
                            {
                                m_logger.Error("Could not find valid JSON config for filter.");
                                return false;
                            }

                            // Deserialize config
                            try
                            {
                                LoadConfigFromJson(cfgJson, s_configSerializerSettings);
                                m_logger.Info("Configuration loaded from JSON.");
                            }
                            catch (Exception deserializationError)
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
                                var listEntry = zip.Entries.Where(pp => pp.FullName.OIEquals(listModel.RelativeListPath)).FirstOrDefault();
                                if (listEntry != null)
                                {
                                    var thisListCategoryName = listModel.RelativeListPath.Substring(0, listModel.RelativeListPath.LastIndexOfAny(new[] { '/', '\\' }) + 1) + Path.GetFileNameWithoutExtension(listModel.RelativeListPath);

                                    MappedFilterListCategoryModel categoryModel = null;

                                    switch (listModel.ListType)
                                    {
                                        case PlainTextFilteringListType.Blacklist:
                                            {
                                                if (TryFetchOrCreateCategoryMap(thisListCategoryName, out categoryModel))
                                                {
                                                    using (var listStream = listEntry.Open())
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
                                                    using (var listStream = listEntry.Open())
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
                                                    using (var listStream = listEntry.Open())
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
                                                using (TextReader tr = new StreamReader(listEntry.Open()))
                                                {
                                                    if (TryFetchOrCreateCategoryMap(thisListCategoryName, out categoryModel))
                                                    {
                                                        var whitelistRules = new List<string>();
                                                        string line = null;
                                                        while ((line = tr.ReadLine()) != null)
                                                        {
                                                            whitelistRules.Add("@@" + line.Trim() + "\n");
                                                        }

                                                        using (var listStream = listEntry.Open())
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
                    }
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

        public bool? DownloadLists()
        {
            return true;
        }

        public bool? VerifyLists()
        {
            return true;
        }

        public bool LoadLists()
        {
            return true;
        }

        private void loadAppList(HashSet<string> myAppList, HashSet<string> appList)
        {
            myAppList.Clear();
            foreach(var app in appList)
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
