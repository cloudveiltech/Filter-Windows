using CloudVeil.IPC.Messages;
using CloudVeil;
using Filter.Platform.Common.Data.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using FilterProvider.Common.Configuration;
using HandlebarsDotNet;
using Filter.Platform.Common.Util;

namespace FilterProvider.Common.Util
{
    public class Templates
    {
        public Templates(IPolicyConfiguration configuration)
        {
            logger = LoggerUtil.GetAppWideLogger();

            policyConfiguration = configuration;

            // Get our blocked HTML page
            byte[] htmlBytes = ResourceStreams.Get("FilterProvider.Common.Resources.BlockedPage.html");
            blockedHtmlPage = Handlebars.Compile(Encoding.UTF8.GetString(htmlBytes));

            badSslHtmlPage = ResourceStreams.Get("FilterProvider.Common.Resources.BadCertPage.html");

            if (blockedHtmlPage == null)
            {
                logger.Error("Could not load packed HTML block page.");
            }

            if (badSslHtmlPage == null)
            {
                logger.Error("Could not load packed HTML bad SSL page.");
            }
        }

        private NLog.Logger logger;

        // Uses Handlebars.Net for compilation of this template function.
        private Func<object, string> blockedHtmlPage;

        private byte[] badSslHtmlPage;

        private IPolicyConfiguration policyConfiguration;

        public byte[] ResolveBadSslTemplate(Uri requestUri, string certThumbprint)
        {
            string pageTemplate = Encoding.UTF8.GetString(badSslHtmlPage);

            // Produces something that looks like "www.badsite.com/example?arg=0" instead of "http://www.badsite.com/example?arg=0"
            // IMO this looks slightly more friendly to a user than the entire URI.
            string friendlyUrlText = (requestUri.Host + requestUri.PathAndQuery + requestUri.Fragment).TrimEnd('/');
            string urlText = requestUri.ToString();

            urlText = urlText == null ? "" : urlText;

            pageTemplate = pageTemplate.Replace("{{url_text}}", urlText);
            pageTemplate = pageTemplate.Replace("{{friendly_url_text}}", friendlyUrlText);
            pageTemplate = pageTemplate.Replace("{{host}}", requestUri.Host);
            pageTemplate = pageTemplate.Replace("{{certThumbprintExists}}", certThumbprint == null ? "false" : "true");
            pageTemplate = pageTemplate.Replace("{{certThumbprint}}", certThumbprint);
            pageTemplate = pageTemplate.Replace("{{serverPort}}", AppSettings.Default.ConfigServerPort.ToString());

            return Encoding.UTF8.GetBytes(pageTemplate);
        }

        public byte[] ResolveBlockedSiteTemplate(Uri requestUri, int matchingCategory, List<MappedFilterListCategoryModel> appliedCategories, BlockType blockType = BlockType.None, string triggerCategory = "")
        {
            Dictionary<string, object> blockPageContext = new Dictionary<string, object>();

            // Produces something that looks like "www.badsite.com/example?arg=0" instead of "http://www.badsite.com/example?arg=0"
            // In my opninion this looks slightly more friendly to a user than the entire URI.
            string friendlyUrlText = (requestUri.Host + requestUri.PathAndQuery + requestUri.Fragment).TrimEnd('/');
            string urlText = requestUri.ToString();

            bool showUnblockRequestButton = true;
            string unblockRequest = getUnblockRequestUrl(urlText);

            string message = "was blocked because it was in the following category:";

            // Collect category information: Blocked category, other categories, and whether the blocked category is in the relaxed policy.
            MappedFilterListCategoryModel matchingCategoryModel = policyConfiguration.GeneratedCategoriesMap.Values.FirstOrDefault(m => m.CategoryId == matchingCategory);
            string matching_category = matchingCategoryModel?.ShortCategoryName;

            List<string> otherCategories = appliedCategories?
                .Where(c => c.CategoryId != matchingCategory)
                .Select(c => c.ShortCategoryName)
                .Distinct()
                .ToList();

            bool isRelaxedPolicy = (matchingCategoryModel is MappedBypassListCategoryModel);

            // Get category or block type.
            string url_text = urlText == null ? "" : urlText;
            if (matchingCategory > 0 && blockType == BlockType.None)
            {
                // matching_category name already set.
            }
            else
            {
                otherCategories = null;
                switch (blockType)
                {
                    case BlockType.None:
                        matching_category = "unknown reason";
                        break;

                    case BlockType.ImageClassification:
                        matching_category = "naughty image";
                        break;

                    case BlockType.Url:
                        matching_category = "bad webpage";
                        break;

                    case BlockType.TextClassification:
                    case BlockType.TextTrigger:
                        matching_category = string.Format("offensive text: {0}", triggerCategory);
                        break;

                    case BlockType.TimeRestriction:
                        message = "is blocked because your time restrictions do not allow internet access at this time.";
                        //matching_category = "no internet allowed after hours";
                        showUnblockRequestButton = false;
                        break;

                    case BlockType.OtherContentClassification:
                    default:
                        matching_category = "other content classification";
                        break;
                }
            }

            blockPageContext.Add("url_text", url_text);
            blockPageContext.Add("friendly_url_text", friendlyUrlText);
            blockPageContext.Add("message", message);
            blockPageContext.Add("matching_category", matching_category);
            blockPageContext.Add("other_categories", otherCategories);
            blockPageContext.Add("showUnblockRequestButton", showUnblockRequestButton);
            blockPageContext.Add("passcodeSetupUrl", CompileSecrets.ServiceProviderUserRelaxedPolicyPath);
            blockPageContext.Add("unblockRequest", unblockRequest);
            blockPageContext.Add("isRelaxedPolicy", isRelaxedPolicy);
            blockPageContext.Add("isRelaxedPolicyPasscodeRequired", policyConfiguration?.Configuration?.EnableRelaxedPolicyPasscode);
            blockPageContext.Add("serverPort", AppSettings.Default.ConfigServerPort);

            return Encoding.UTF8.GetBytes(blockedHtmlPage(blockPageContext));
        }

        private static string getUnblockRequestUrl(string urlText)
        {
            string deviceName;

            try
            {
                deviceName = Environment.MachineName;
            }
            catch
            {
                deviceName = "Unknown";
            }

            string blockedRequestBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(urlText));

            string unblockRequest = WebServiceUtil.Default.ServiceProviderUnblockRequestPath;
            string username = WebServiceUtil.Default.UserEmail ?? "DNS";

            string query = string.Format("category_name=LOOKUP_UNKNOWN&user_id={0}&device_name={1}&blocked_request={2}", Uri.EscapeDataString(username), deviceName, Uri.EscapeDataString(blockedRequestBase64));
            unblockRequest += "?" + query;

            return unblockRequest;
        }
    }
}
