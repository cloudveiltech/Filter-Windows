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
        /// <returns>false if configuration was not downloaded. null if configuration endpoint was not accessible.</returns>
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

        /// <summary>
        /// This method should verify the lists downloaded to make sure they are the right lists and have not been tampered with.
        /// </summary>
        /// <returns>null if no internet, true if lists were successfully verified. false if lists need reload.</returns>
        bool? VerifyLists();

        /// <summary>
        /// This method should download the lists that the authenticated user's policy requires.
        /// </summary>
        /// <returns>null if no internet, true if lists were successfully downloaded. false if lists were not needed</returns>
        bool? DownloadLists();

        /// <summary>
        /// This method should load the required lists from the disk.
        /// </summary>
        /// <returns></returns>
        bool LoadLists();

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
