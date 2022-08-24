﻿/*
* Copyright © 2017-2018 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CloudVeil.Core.Extensions;
using Filter.Platform.Common;
using Filter.Platform.Common.Extensions;
using Filter.Platform.Common.Util;
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

namespace CloudVeil.Core.Windows.Util.Update
{
    /// <summary>
    /// The AppcastUpdater class checks for application updates at a URI supplied at the construction
    /// of an instance. This class exposes a single public member which, when called, will either
    /// return the highest available application upgrade if one could be found, or null of no better
    /// version than the current executing assembly could be found.
    /// </summary>
    public class AppcastUpdater
    {
        private Uri appcastLocationUri;

        private Logger logger;

        /// <summary>
        /// Constructs a new AppcastUpdater with the given URI, a URI to an appcast this class will
        /// use to search for updates.
        /// </summary>
        /// <param name="appcastLocation">
        /// The URI to an appcast that will give us potential update information.
        /// </param>
        public AppcastUpdater(Uri appcastLocation)
        {
            appcastLocationUri = appcastLocation;

            logger = LoggerUtil.GetAppWideLogger();
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
        /// Returns an ApplicationUpdate if one is found.
        /// </returns>
        public async Task<ApplicationUpdate> GetLatestUpdate(string myUpdateChannel = null)
        {
            var thisVersion = Assembly.GetEntryAssembly().GetName().Version;
            var bestVersion = thisVersion;

            ApplicationUpdate bestAvailableUpdate = null;

            try
            {
                using (var cli = new HttpClient())
                {
                    string appInfo = null;

#if USE_LOCAL_UPDATE_XML
                    string appInfoPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"CloudVeil", @"update.xml");
                    if(!File.Exists(appInfoPath))
                    {
                        appInfo = await cli.GetStringAsync(appcastLocationUri);
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
                    appInfo = await cli.GetStringAsync(appcastLocationUri);
#endif
                    var feed = SyndicationFeed.Load(XmlReader.Create(new StringReader(appInfo)));

                    foreach (var item in feed.Items)
                    {
                        var enclosure = item.Links.Where(x => x.RelationshipType == "enclosure").FirstOrDefault();

                        if (enclosure != null)
                        {
                            var sparkleVersion = enclosure.AttributeExtensions.Where(x => x.Key.Name == "version").FirstOrDefault().Value;
                            var sparkleOs = enclosure.AttributeExtensions.Where(x => x.Key.Name == "os").FirstOrDefault().Value;
                            var updateChannel = enclosure.AttributeExtensions.Where(x => x.Key.Name == "channel").FirstOrDefault().Value;
                            var sparkleInstallerArgs = enclosure.AttributeExtensions.Where(x => x.Key.Name == "installerArguments").FirstOrDefault().Value;

                            if (StringExtensions.Valid(updateChannel) && StringExtensions.Valid(myUpdateChannel) && !updateChannel.OIEquals(myUpdateChannel))
                            {
                                logger.Info($"Skipping app update in channel {updateChannel} because it doesn't match required channel {myUpdateChannel}.", updateChannel, myUpdateChannel);
                                continue;
                            }

                            string extension = Path.GetExtension(enclosure.Uri.ToString());
                            UpdateKind updateKind = extension == ".exe" ? UpdateKind.ExecutablePackage : UpdateKind.InstallerPackage;

                            Uri url = enclosure.Uri;
                            long length = enclosure.Length;
                            string mediaType = enclosure.MediaType;

                            var thisUpdateVersion = Version.Parse(sparkleVersion);

                            bestAvailableUpdate = new ApplicationUpdate(item.PublishDate.DateTime, item.Title.Text, ((TextSyndicationContent)item.Content).Text, thisVersion, thisUpdateVersion, url, updateKind, sparkleInstallerArgs, sparkleInstallerArgs.IndexOf("norestart") < 0);
                            bestVersion = thisUpdateVersion;
                        }
                    }
                }
            }
            catch (WebException we)
            {
                // Failed to load. Doesn't matter, we could not find an update. Maybe later
                // return an error or something.
                LoggerUtil.RecursivelyLogException(logger, we);
            }
            catch (Exception e)
            {
                // Other unknown failure. Doesn't matter, we could not find an update. Maybe later
                // return an error or something.
                LoggerUtil.RecursivelyLogException(logger, e);
            }

            return bestAvailableUpdate;
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
            ApplicationUpdate update = await GetLatestUpdate(myUpdateChannel);

            var thisVersion = Assembly.GetEntryAssembly().GetName().Version;

            if(update.IsNewerThan(thisVersion))
            {
                return update;
            }
            else
            {
                return null;
            }
        }
    }
}