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

        public byte[] ResolveBlockedSiteTemplate(Uri requestUri, int matchingCategory, List<MappedFilterListCategoryModel> appliedCategories, BlockType blockType = BlockType.None, string triggerCategory = "", string triggerText = "")
        {
            Dictionary<string, object> blockPageContext = new Dictionary<string, object>();

            // Produces something that looks like "www.badsite.com/example?arg=0" instead of "http://www.badsite.com/example?arg=0"
            // In my opninion this looks slightly more friendly to a user than the entire URI.
            string friendlyUrlText = (requestUri.Host + requestUri.PathAndQuery + requestUri.Fragment).TrimEnd('/');
            string urlText = requestUri.ToString();

            bool showUnblockRequestButton = true;

            string message = "was blocked because it was in the following category:";

            // Collect category information: Blocked category, other categories, and whether the blocked category is in the relaxed policy.
            MappedFilterListCategoryModel matchingCategoryModel = policyConfiguration.GeneratedCategoriesMap.Values.FirstOrDefault(m => m.CategoryId == matchingCategory);
            string matchingCatergoryName = matchingCategoryModel?.ShortCategoryName;

            List<string> otherCategories = appliedCategories?
                .Where(c => c.CategoryId != matchingCategory)
                .Select(c => c.ShortCategoryName)
                .Distinct()
                .ToList();

            bool isRelaxedPolicy = (matchingCategoryModel is MappedBypassListCategoryModel);

            string unblockRequest = getUnblockRequestUrl(urlText, triggerText, matchingCatergoryName);

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
                        matchingCatergoryName = "unknown reason";
                        break;

                    case BlockType.ImageClassification:
                        matchingCatergoryName = "naughty image";
                        break;

                    case BlockType.Url:
                        matchingCatergoryName = "bad webpage";
                        break;

                    case BlockType.TextClassification:
                    case BlockType.TextTrigger:
                        matchingCatergoryName = string.Format("offensive text: {0}", triggerCategory);
                        break;

                    case BlockType.TimeRestriction:
                        message = "is blocked because your time restrictions do not allow internet access at this time.";
                        //matching_category = "no internet allowed after hours";
                        showUnblockRequestButton = false;
                        break;

                    case BlockType.OtherContentClassification:
                    default:
                        matchingCatergoryName = "other content classification";
                        break;
                }
            }
            
            blockPageContext.Add("url_text", url_text);
            blockPageContext.Add("friendly_url_text", friendlyUrlText);
            blockPageContext.Add("message", message);
            blockPageContext.Add("matching_category", matchingCatergoryName);
            blockPageContext.Add("other_categories", otherCategories);
            blockPageContext.Add("showUnblockRequestButton", showUnblockRequestButton);
            blockPageContext.Add("passcodeSetupUrl", CompileSecrets.ServiceProviderUserRelaxedPolicyPath);
            blockPageContext.Add("unblockRequest", unblockRequest);
            blockPageContext.Add("isRelaxedPolicy", isRelaxedPolicy);
            blockPageContext.Add("isRelaxedPolicyPasscodeRequired", policyConfiguration?.Configuration?.EnableRelaxedPolicyPasscode);
            blockPageContext.Add("serverPort", AppSettings.Default.ConfigServerPort);

            return Encoding.UTF8.GetBytes(blockedHtmlPage(blockPageContext));
        }

        public static string getUnblockRequestUrl(string blockedUrl, string blockedTerm, string category)
        {
            var userEmail = WebServiceUtil.Default.UserEmail; 
            string deviceName = string.Empty;
            try
            {
                deviceName = Environment.MachineName;
            }
            catch
            {
                deviceName = "Unknown";
            }

            var reportPath = WebServiceUtil.Default.ServiceProviderUnblockRequestPath;
            return string.Format(
                @"{0}?category_name={1}&user_id={2}&device_name={3}&blocked_request={4}&platform=cv4w&token={5}&trigger={6}",
                reportPath,
                Uri.EscapeDataString(category),
                Uri.EscapeDataString(userEmail),
                Uri.EscapeDataString(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(deviceName))),
                Uri.EscapeDataString(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(blockedUrl))),
                Uri.EscapeDataString(WebServiceUtil.Default.AuthId),
                Uri.EscapeDataString(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(blockedTerm)))
                );
        }
    }
}
