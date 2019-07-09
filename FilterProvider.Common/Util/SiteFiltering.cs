using Citadel.IPC;
using Citadel.IPC.Messages;
using DistillNET;
using Filter.Platform.Common.Data.Models;
using Filter.Platform.Common.Util;
using FilterProvider.Common.Configuration;
using FilterProvider.Common.Data;
using GoproxyWrapper;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Linq;
using System.Threading;
using System.Net;
using CloudVeil;
using System.Diagnostics;
using GoProxyWrapper;

namespace FilterProvider.Common.Util
{
    public delegate void RequestBlockedHandler(short category, BlockType cause, Uri requestUri, string matchingRule);

    public class SiteFiltering
    {
        public SiteFiltering(IPCServer ipcServer, TimeDetection timeDetection, IPolicyConfiguration policyConfiguration, CertificateExemptions certificateExemptions, ReaderWriterLockSlim filteringRwLock)
        {
            m_ipcServer = ipcServer;
            m_timeDetection = timeDetection;
            m_policyConfiguration = policyConfiguration;
            m_filteringRwLock = filteringRwLock;

            m_logger = LoggerUtil.GetAppWideLogger();
            m_templates = new Templates(policyConfiguration);

            m_certificateExemptions = certificateExemptions;

            m_globalBlacklistFiltersCache = null;
            m_globalWhitelistFiltersCache = null;

            m_policyConfiguration.ListsReloaded += OnListsReloaded;
        }

        private ReaderWriterLockSlim m_filteringRwLock;

        private NLog.Logger m_logger;

        private IPCServer m_ipcServer;

        private CertificateExemptions m_certificateExemptions;

        private TimeDetection m_timeDetection;

        private Templates m_templates;

        private object m_filterCacheLock = new object();
        private List<UrlFilter> m_globalWhitelistFiltersCache;

        private List<UrlFilter> m_globalBlacklistFiltersCache;

        private IPolicyConfiguration m_policyConfiguration;

        public event RequestBlockedHandler RequestBlocked;

        private void OnListsReloaded(object sender, EventArgs e)
        {
            /*List<UrlFilter> blacklist, whitelist;

            blacklist = m_policyConfiguration?.FilterCollection?.GetFiltersForDomain()?.Result;
            whitelist = m_policyConfiguration?.FilterCollection?.GetWhitelistFiltersForDomain()?.Result;

            lock (m_filterCacheLock)
            {
                m_globalBlacklistFiltersCache = blacklist;
                m_globalWhitelistFiltersCache = whitelist;
            }*/
        }

        /// <summary>
        /// Builds up a host from hostParts and checks the bloom filter for each entry.
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="hostParts"></param>
        /// <param name="isWhitelist"></param>
        /// <returns>true if any host is discovered in the collection.</returns>
        private bool isHostInList(FilterDbCollection collection, string[] hostParts, bool isWhitelist)
        {
            int i = hostParts.Length > 1 ? hostParts.Length - 2 : hostParts.Length - 1;
            for (; i >= 0; i--)
            {
                string checkHost = string.Join(".", new ArraySegment<string>(hostParts, i, hostParts.Length - i));
                bool result = collection.PrefetchIsDomainInList(checkHost, isWhitelist);

                if (result)
                {
                    return true;
                }
            }

            return false;
        }

        private static readonly string[] htmlMimeTypes = { "text/html", "text/plain" };
        private static int nextTrackId = 1;
        private static object trackIdLock = new object();

        /// <summary>
        /// Called when each request is intercepted and has not gone to the server yet.
        /// </summary>
        internal ProxyNextAction OnBeforeRequest(GoproxyWrapper.Session args)
        {
            ProxyNextAction nextAction = ProxyNextAction.AllowAndIgnoreContent;
            int trackId = 0;
            lock (trackIdLock)
            {
                trackId = nextTrackId++;
            }

            // Don't allow filtering if our user has been denied access and they
            // have not logged back in.
            if (m_ipcServer != null && m_ipcServer.WaitingForAuth)
            {
                return ProxyNextAction.AllowAndIgnoreContentAndResponse;
            }

            try
            {
                ZonedDateTime date = m_timeDetection.GetRealTime();
                TimeRestrictionModel todayRestriction = null;

                if (m_policyConfiguration != null && m_policyConfiguration.TimeRestrictions != null && date != null)
                {
                    todayRestriction = m_policyConfiguration.TimeRestrictions[(int)date.ToDateTimeOffset().DayOfWeek];
                }

                string urlString = args.Request.Url;
                Uri url = new Uri(urlString);
                Uri serviceProviderPath = new Uri(CompileSecrets.ServiceProviderApiPath);

                if (url.Host == serviceProviderPath.Host)
                {
                    return ProxyNextAction.AllowAndIgnoreContentAndResponse;
                }
                else if (todayRestriction != null && todayRestriction.RestrictionsEnabled && !m_timeDetection.IsDateTimeAllowed(date, todayRestriction))
                {
                    sendBlockResponse(args, urlString, null, BlockType.TimeRestriction);
                    return ProxyNextAction.DropConnection;
                }
              
            }
            catch (Exception e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }

            return nextAction;
        }

        private bool useHtmlBlockPage(Session args)
        {
            Header acceptHeader = args.Request.Headers.GetFirstHeader("Accept");

            bool useHtmlBlockPage = false;

            if (acceptHeader != null)
            {
                foreach (string headerVal in htmlMimeTypes)
                {
                    if (acceptHeader.Value.IndexOf(headerVal, StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        useHtmlBlockPage = true;
                        break;
                    }
                }
            }
            else
            {
                // We don't know what the client is accepting. Make it obvious what's going on by spitting an error onto the screen.
                useHtmlBlockPage = true;
            }

            if (!args.Request.Headers.HeaderExists("Referer"))
            {
                // Use the HTML block page if this is a direct navigation.
                useHtmlBlockPage = true;
            }

            return useHtmlBlockPage;
        }

        private void sendBlockResponse(Session args, string url, int[] categories, BlockType blockType = BlockType.Url, string triggerCategory = "")
        {
            bool useHtmlBlockPage = this.useHtmlBlockPage(args);

            if (useHtmlBlockPage)
            {
                int matchCategory = (categories != null && categories.Length > 0) ? categories[0] : 0;

                List<MappedFilterListCategoryModel> appliedCategories = null;
                if(categories == null)
                {
                    appliedCategories = null;
                }
                else
                {
                    appliedCategories = categories
                        .Skip(1)
                        .Select(
                            id => m_policyConfiguration.GeneratedCategoriesMap
                                        .Select(c => c.Value)
                                        .FirstOrDefault(c => c.CategoryId == id)
                        ).ToList();
                }

                byte[] contentBytes = m_templates.ResolveBlockedSiteTemplate(new Uri(url), matchCategory, appliedCategories, blockType, triggerCategory);
                string contentType = "text/html";

                args.SendCustomResponse((int)HttpStatusCode.OK, contentType, contentBytes);
            }
            else
            {
                args.SendCustomResponse((int)HttpStatusCode.NoContent, "", new byte[0]);
            }
        }

        public int OnWhitelist(Session session, string url, int[] categories)
        {
            try
            {
                var mappedCategory = m_policyConfiguration.GeneratedCategoriesMap.FirstOrDefault(m => m.Value.CategoryId == categories[0]).Value;

                m_logger.Info("Request {0} whitelisted in category {1} (rule not currently available)", url, mappedCategory?.CategoryName);

                return 0;
            }
            catch(Exception ex)
            {
                m_logger.Error("Exception occurred while processing whitelist notification.");
                LoggerUtil.RecursivelyLogException(m_logger, ex);
            }

            return 0;
        }

        public int OnBlacklist(Session args, string url, int[] categories)
        {
            try
            {
                m_logger.Info("OnBlacklist {0}", url);

                RequestBlocked?.Invoke((short)categories[0], BlockType.Url, new Uri(url), "NOT AVAILABLE");

                sendBlockResponse(args, url, categories);
            }
            catch(Exception ex)
            {
                m_logger.Error("Exception occurred while processing blacklist notification.");
                LoggerUtil.RecursivelyLogException(m_logger, ex);
            }

            return 0;
        }

        internal void OnBeforeResponse(GoproxyWrapper.Session args)
        {
            ProxyNextAction nextAction = ProxyNextAction.AllowButRequestContentInspection;

            bool shouldBlock = false;
            string customBlockResponseContentType = null;
            byte[] customBlockResponse = null;

            // Don't allow filtering if our user has been denied access and they
            // have not logged back in.
            if (m_ipcServer != null && m_ipcServer.WaitingForAuth)
            {
                return;
            }

            // The only thing we can really do in this callback, and the only thing we care to do, is
            // try to classify the content of the response payload, if there is any.
            try
            {
                Uri uri = new Uri(args.Request.Url);
                Uri serviceProviderPath = new Uri(CompileSecrets.ServiceProviderApiPath);

                if(uri.Host == serviceProviderPath.Host)
                {
                    return;
                }

                // Check our certificate exemptions to see if we should allow this site through or not.
                if (args.Response.CertificateCount > 0 && !args.Response.IsCertificateVerified && !m_certificateExemptions.IsExempted(uri.Host, args.Response.Certificates[0]))
                {
                    customBlockResponseContentType = "text/html";
                    customBlockResponse = m_templates.ResolveBadSslTemplate(new Uri(args.Request.Url), args.Response.Certificates[0].Thumbprint);
                    nextAction = ProxyNextAction.DropConnection;
                    return;
                }

                string contentType = null;
                if (args.Response.Headers.HeaderExists("Content-Type"))
                {
                    contentType = args.Response.Headers.GetFirstHeader("Content-Type").Value;

                    bool isHtml = contentType.IndexOf("html") != -1;
                    bool isJson = contentType.IndexOf("json") != -1;
                    bool isTextPlain = contentType.IndexOf("text/plain") != -1;

                    // Is the response content type text/html or application/json? Inspect it, otherwise return before we do content classification.
                    // Why enforce content classification on only these two? There are only a few MIME types which have a high risk of "carrying" explicit content.
                    // Those are:
                    // text/plain
                    // text/html
                    // application/json
                    if (!(isHtml || isJson || isTextPlain))
                    {
                        return;
                    }
                }

                if (contentType != null && args.Response.HasBody)
                {
                    contentType = contentType.ToLower();

                    BlockType blockType;
                    string textTrigger;
                    string textCategory;

                    byte[] responseBody = args.Response.Body;
                    var contentClassResult = OnClassifyContent(responseBody, contentType, out blockType, out textTrigger, out textCategory);

                    if (contentClassResult > 0)
                    {
                        shouldBlock = true;

                        List<MappedFilterListCategoryModel> categories = new List<MappedFilterListCategoryModel>();

                        nextAction = ProxyNextAction.DropConnection;

                        if (contentType.IndexOf("html") != -1)
                        {
                            customBlockResponseContentType = "text/html";
                            customBlockResponse = m_templates.ResolveBlockedSiteTemplate(new Uri(args.Request.Url), contentClassResult, categories, blockType, textCategory);
                        }
                        else if (contentType.IndexOf("application/json", StringComparison.InvariantCultureIgnoreCase) != -1)
                        {
                            customBlockResponseContentType = "application/json";
                            customBlockResponse = new byte[0];
                        }

                        RequestBlocked?.Invoke(contentClassResult, blockType, new Uri(args.Request.Url), "");
                        m_logger.Info("Response blocked by content classification.");
                    }
                }
            }
            catch (Exception e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
            finally
            {
                if (nextAction == ProxyNextAction.DropConnection)
                {
                    // TODO: Do we really need this as Info?
                    m_logger.Info("Response blocked: {0}", args.Request.Url);

                    if (customBlockResponse != null)
                    {
                        args.SendCustomResponse((int)HttpStatusCode.OK, customBlockResponseContentType, customBlockResponse);
                    }
                }
            }
        }

        private List<MappedFilterListCategoryModel> ResolveCategoriesFromIds(List<int> matchingCategories)
        {
            List<MappedFilterListCategoryModel> categories = new List<MappedFilterListCategoryModel>();

            int length = matchingCategories.Count;
            var categoryValues = m_policyConfiguration.GeneratedCategoriesMap.Values;
            foreach (var category in categoryValues)
            {
                for (int i = 0; i < length; i++)
                {
                    if (category.CategoryId == matchingCategories[i])
                    {
                        categories.Add(category);
                    }
                }
            }

            return categories;
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
        private short OnClassifyContent(Memory<byte> data, string contentType, out BlockType blockedBecause, out string textTrigger, out string triggerCategory)
        {
            Stopwatch stopwatch = null;

            try
            {
                m_filteringRwLock.EnterReadLock();

                stopwatch = Stopwatch.StartNew();
                if (m_policyConfiguration.TextTriggers != null && m_policyConfiguration.TextTriggers.HasTriggers)
                {
                    var isHtml = contentType.IndexOf("html") != -1;
                    var isJson = contentType.IndexOf("json") != -1;
                    if (isHtml || isJson)
                    {
                        var dataToAnalyzeStr = Encoding.UTF8.GetString(data.ToArray());

                        if (isHtml)
                        {
                            // This doesn't work anymore because google has started sending bad stuff directly
                            // embedded inside HTML responses, instead of sending JSON a separate response.
                            // So, we need to let the triggers engine just chew through the entire raw HTML.
                            // var ext = new FastHtmlTextExtractor();
                            // dataToAnalyzeStr = ext.Extract(dataToAnalyzeStr.ToCharArray(), true);
                        }

                        short matchedCategory = -1;
                        string trigger = null;
                        var cfg = m_policyConfiguration.Configuration;

                        if (m_policyConfiguration.TextTriggers.ContainsTrigger(dataToAnalyzeStr, out matchedCategory, out trigger, m_policyConfiguration.CategoryIndex.GetIsCategoryEnabled, cfg != null && cfg.MaxTextTriggerScanningSize > 1, cfg != null ? cfg.MaxTextTriggerScanningSize : -1))
                        {
                            m_logger.Info("Triggers successfully run. matchedCategory = {0}, trigger = '{1}'", matchedCategory, trigger);

                            var mappedCategory = m_policyConfiguration.GeneratedCategoriesMap.Values.Where(xx => xx.CategoryId == matchedCategory).FirstOrDefault();

                            if (mappedCategory != null)
                            {
                                m_logger.Info("Response blocked by text trigger \"{0}\" in category {1}.", trigger, mappedCategory.CategoryName);
                                blockedBecause = BlockType.TextTrigger;
                                triggerCategory = mappedCategory.CategoryName;
                                textTrigger = trigger;
                                return mappedCategory.CategoryId;
                            }
                        }
                    }
                }
                stopwatch.Stop();

                //m_logger.Info("Text triggers took {0} on {1}", stopwatch.ElapsedMilliseconds, url);
            }
            catch (Exception e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
            finally
            {
                m_filteringRwLock.ExitReadLock();
            }

#if WITH_NLP
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
                                    var cfg = m_policyConfiguration.Configuration;
                                    var threshold = cfg != null ? cfg.NlpThreshold : 0.9f;

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

#endif
            // Default to zero. Means don't block this content.
            blockedBecause = BlockType.OtherContentClassification;
            textTrigger = "";
            triggerCategory = "";
            return 0;
        }

        #region Block Page Templates

        

        #endregion
    }
}
