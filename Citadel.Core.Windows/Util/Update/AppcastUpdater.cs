/*
* Copyright © 2017 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Citadel.Core.Extensions;
using NLog;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.ServiceModel.Syndication;
using System.Threading.Tasks;
using System.Xml;

namespace Citadel.Core.Windows.Util.Update
{
    /// <summary>
    /// The AppcastUpdater class checks for application updates at a URI supplied at the construction
    /// of an instance. This class exposes a single public member which, when called, will either
    /// return the highest available application upgrade if one could be found, or null of no better
    /// version than the current executing assembly could be found.
    /// </summary>
    public class AppcastUpdater
    {
        private Uri m_appcastLocationUri;

        private Logger m_logger;

        /// <summary>
        /// Constructs a new AppcastUpdater with the given URI, a URI to an appcast this class will
        /// use to search for updates.
        /// </summary>
        /// <param name="appcastLocation">
        /// The URI to an appcast that will give us potential update information.
        /// </param>
        public AppcastUpdater(Uri appcastLocation)
        {
            m_appcastLocationUri = appcastLocation;

            m_logger = LoggerUtil.GetAppWideLogger();
        }

        /// <summary>
        /// Checks for application updates broadcasted by the appcast URI provided when this object
        /// was constructed.
        /// </summary>
        /// <param name="myUpdateChannel">
        /// An optional update channel. Will only look for updates where the channel of the update
        /// matches this value in a case-insensitive fashion.
        /// </param>
        /// <returns>
        /// Returns an ApplicationUpdate if an update that supersedes the executing assembly version
        /// could be found. Returns null otherwise.
        /// </returns>
        public async Task<ApplicationUpdate> CheckForUpdate(string myUpdateChannel = null)
        {
            var thisVersion = Assembly.GetEntryAssembly().GetName().Version;
            var bestVersion = thisVersion;

            ApplicationUpdate bestAvailableUpdate = null;

            try
            {
                using(var cli = new HttpClient())
                {
                    string appInfo = null;

#if USE_LOCAL_UPDATE_XML
                    string appInfoPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"CloudVeil", @"update.xml");
                    if(File.Exists(appInfoPath))
                    {
                        appInfo = await cli.GetStringAsync(m_appcastLocationUri);
                    }
                    else
                    {
                        using (var stream = new FileStream(appInfoPath, FileMode.Open))
                        {
                            using (var reader = new StreamReader(stream))
                            {
                                appInfo = await reader.ReadToEndAsync();
                            }
                        }
                    }
#else
                    appInfo = await cli.GetStringAsync(m_appcastLocationUri);
#endif

                    var feed = SyndicationFeed.Load(XmlReader.Create(new StringReader(appInfo)));

                    foreach(var item in feed.Items)
                    {
                        var enclosure = item.Links.Where(x => x.RelationshipType == "enclosure").FirstOrDefault();

                        if(enclosure != null)
                        {
                            var sparkleVersion = enclosure.AttributeExtensions.Where(x => x.Key.Name == "version").FirstOrDefault().Value;
                            var sparkleOs = enclosure.AttributeExtensions.Where(x => x.Key.Name == "os").FirstOrDefault().Value;
                            var updateChannel = enclosure.AttributeExtensions.Where(x => x.Key.Name == "channel").FirstOrDefault().Value;
                            var sparkleInstallerArgs = enclosure.AttributeExtensions.Where(x => x.Key.Name == "installerArguments").FirstOrDefault().Value;

                            if(StringExtensions.Valid(updateChannel) && StringExtensions.Valid(myUpdateChannel) && !updateChannel.OIEquals(myUpdateChannel))
                            {
                                m_logger.Info("Skipping app update in channel {0} because it doesn't match required channel {1}.", updateChannel, myUpdateChannel);
                                continue;
                            }

                            Uri url = enclosure.Uri;
                            long length = enclosure.Length;
                            string mediaType = enclosure.MediaType;

                            var thisUpdateVersion = Version.Parse(sparkleVersion);

                            m_logger.Info("App version {0} detected in update channel. Checking if superior.", sparkleVersion);

                            if(thisUpdateVersion > bestVersion)
                            {
                                m_logger.Info("Available app update with version {0} is superior to current best version {1}.", bestVersion.ToString());

                                bestAvailableUpdate = new ApplicationUpdate(item.PublishDate.DateTime, item.Title.Text, ((TextSyndicationContent)item.Content).Text, thisVersion, thisUpdateVersion, url, UpdateKind.MsiInstaller, sparkleInstallerArgs, sparkleInstallerArgs.IndexOf("norestart") < 0);
                                bestVersion = thisUpdateVersion;
                            }
                        }
                    }
                }
            }
            catch(WebException we)
            {
                // Failed to load. Doesn't matter, we could not find an update. Maybe later
                // return an error or something.
                LoggerUtil.RecursivelyLogException(m_logger, we);
            }
            catch(Exception e)
            {
                // Other unknown failure. Doesn't matter, we could not find an update. Maybe later
                // return an error or something.
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }

            return bestAvailableUpdate;
        }
    }
}