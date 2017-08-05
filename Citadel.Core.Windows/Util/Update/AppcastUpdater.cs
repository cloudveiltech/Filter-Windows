/*
* Copyright © 2017 Jesse Nicholson  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

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
        }

        /// <summary>
        /// Checks for application updates broadcasted by the appcast URI provided when this object
        /// was constructed.
        /// </summary>
        /// <returns>
        /// Returns an ApplicationUpdate if an update that supersedes the executing assembly version
        /// could be found. Returns null otherwise.
        /// </returns>
        public async Task<ApplicationUpdate> CheckForUpdate()
        {
            var thisVersion = Assembly.GetEntryAssembly().GetName().Version;
            var bestVersion = thisVersion;

            ApplicationUpdate bestAvailableUpdate = null;

            try
            {
                using(var cli = new HttpClient())
                {

                    var appInfo = await cli.GetStringAsync(m_appcastLocationUri);

                    var feed = SyndicationFeed.Load(XmlReader.Create(new StringReader(appInfo)));

                    foreach(var item in feed.Items)
                    {
                        var enclosure = item.Links.Where(x => x.RelationshipType == "enclosure").FirstOrDefault();

                        if(enclosure != null)
                        {
                            var sparkleVersion = enclosure.AttributeExtensions.Where(x => x.Key.Name == "version").FirstOrDefault().Value;
                            var sparkleOs = enclosure.AttributeExtensions.Where(x => x.Key.Name == "os").FirstOrDefault().Value;
                            var sparkleInstallerArgs = enclosure.AttributeExtensions.Where(x => x.Key.Name == "installerArguments").FirstOrDefault().Value;

                            Uri url = enclosure.Uri;
                            long length = enclosure.Length;
                            string mediaType = enclosure.MediaType;

                            var thisUpdateVersion = Version.Parse(sparkleVersion);

                            if(thisUpdateVersion > bestVersion)
                            {
                                bestAvailableUpdate = new ApplicationUpdate(item.PublishDate.DateTime, item.Title.Text, ((TextSyndicationContent)item.Content).Text, thisVersion, thisUpdateVersion, url, UpdateKind.MsiInstaller, sparkleInstallerArgs);
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
            }
            catch(Exception e)
            {
                // Other unknown failure. Doesn't matter, we could not find an update. Maybe later
                // return an error or something.
            }

            return bestAvailableUpdate;
        }
    }
}