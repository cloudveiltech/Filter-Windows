using CitadelService.Data.Filtering;
using CitadelService.Data.Models;
using DistillNET;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CitadelService.Common.Configuration
{
    public interface IPolicyConfiguration
    {
        /// <summary>
        /// This method should download the authenticated user's configuration from the server.
        /// </summary>
        /// <returns>false if configuration was not downloaded.</returns>
        bool? DownloadConfiguration();

        /// <summary>
        /// This method should load the authenticated user's configuration from disk.
        /// </summary>
        /// <returns>false if no configuration was found. true if user configuration was found.</returns>
        bool LoadConfiguration();

        /// <summary>
        /// This method should verify the disk configuration contents with the server.
        /// </summary>
        /// <returns>null if no internet, true if configuration successfully verified. false if configuration needs reload.</returns>
        bool? VerifyConfiguration();

        event EventHandler OnConfigurationLoaded;

        AppConfigModel Configuration { get; set; }

        FilterDbCollection FilterCollection { get; }

        BagOfTextTriggers TextTriggers { get; }
       
        HashSet<string> BlacklistedApplications { get; }
        HashSet<string> WhitelistedApplications { get; }

        CategoryIndex CategoryIndex { get; }

        ConcurrentDictionary<string, MappedFilterListCategoryModel> GeneratedCategoriesMap { get; }
    }
}
