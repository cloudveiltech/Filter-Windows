using DistillNET;
using DotNet.Globbing;
using Filter.Platform.Common.Data.Models;
using FilterProvider.Common.Configuration;
using FilterProvider.Common.Data.Filtering;
using Filter.Platform.Common.Data.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FilterServiceTests.Mocks
{
    class MockPolicyConfiguration : IPolicyConfiguration
    {
        public AppConfigModel Configuration { get; set; }

        public FilterDbCollection FilterCollection { get; set; }

        public BagOfTextTriggers TextTriggers { get; set; }

        public HashSet<string> BlacklistedApplications { get; set; }

        public HashSet<string> WhitelistedApplications { get; set; }

        public HashSet<Glob> BlacklistedApplicationGlobs { get; set; } = new HashSet<Glob>();

        public HashSet<Glob> WhitelistedApplicationGlobs { get; set; } = new HashSet<Glob>();

        public CategoryIndex CategoryIndex { get; set; }

        public ConcurrentDictionary<string, MappedFilterListCategoryModel> GeneratedCategoriesMap { get; set; }

        public TimeRestrictionModel[] TimeRestrictions { get; set; }

        public bool AreAnyTimeRestrictionsEnabled { get; set; }

        public event EventHandler OnConfigurationLoaded;

        public bool? DownloadConfiguration()
        {
            throw new NotImplementedException();
        }

        public bool? DownloadLists()
        {
            throw new NotImplementedException();
        }

        public bool LoadConfiguration()
        {
            throw new NotImplementedException();
        }

        public bool LoadLists()
        {
            throw new NotImplementedException();
        }

        public bool? VerifyConfiguration()
        {
            throw new NotImplementedException();
        }

        public bool? VerifyLists()
        {
            throw new NotImplementedException();
        }
    }
}
