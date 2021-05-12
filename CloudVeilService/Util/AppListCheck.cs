using DotNet.Globbing;
using FilterProvider.Common.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CitadelService.Util
{
    public class AppListCheck
    {
        public AppListCheck(IPolicyConfiguration configuration)
        {
            this.configuration = configuration;
        }

        private IPolicyConfiguration configuration;

        public bool IsAppInWhitelist(string appAbsolutePath, string appName)
        {
            return IsAppInList(configuration?.WhitelistedApplications, configuration?.WhitelistedApplicationGlobs, appAbsolutePath, appName);
        }

        public bool IsAppInBlacklist(string appAbsolutePath, string appName)
        {
            return IsAppInList(configuration?.BlacklistedApplications, configuration?.BlacklistedApplicationGlobs, appAbsolutePath, appName);
        }

        /// <summary>
        /// A little helper function for finding a path in a whitelist/blacklist.
        /// </summary>
        /// <param name="list"></param>
        /// <param name="appAbsolutePath"></param>
        /// <param name="appName"></param>
        /// <returns></returns>
        public bool IsAppInList(HashSet<string> list, HashSet<Glob> globs, string appAbsolutePath, string appName)
        {
            if (list.Contains(appName))
            {
                // Whitelist is in effect and this app is whitelisted. So, don't force it through.
                return true;
            }

            // Support for whitelisted apps like Android Studio\bin\jre\java.exe
            foreach (string app in list)
            {
                // Windows isn't case-sensitive, we shouldn't be either.
                if (app.Contains(Path.DirectorySeparatorChar) && appAbsolutePath.EndsWith(app, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (globs != null && globs.Count > 0)
            {
                foreach(var glob in globs)
                {
                    if(glob.IsMatch(appAbsolutePath))
                    {
                        return true;
                    }
                }

                return false;

                // globs.Any(g => g.IsMatch(appAbsolutePath);
            }

            return false;
        }
    }
}
