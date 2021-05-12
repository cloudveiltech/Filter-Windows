/*
* Copyright © 2017-2018 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Filter.Platform.Common;

namespace CloudVeil.Core.Windows.Util.Update
{
    [Serializable]
    public enum UpdateKind
    {
        /// <summary>
        /// This update kind indicates that the installer file provided is intended for use with the platform's installer framework
        /// (think msiexec for Windows, dpkg for Ubuntu, etc.
        /// </summary>
        InstallerPackage,

        /// <summary>
        /// This update kind indicates that the installer file provided is a zip archive.
        /// </summary>
        Archive,

        /// <summary>
        /// This update kind indicates that the installer file provided is intended to be directly run by the system.
        /// </summary>
        ExecutablePackage
    }

    [Serializable]
    public class ApplicationUpdate
    {
        public DateTime DatePublished
        {
            get;
            private set;
        }

        public string Title
        {
            get;
            private set;
        }

        public string HtmlBody
        {
            get;
            private set;
        }

        public Version CurrentVersion
        {
            get;
            private set;
        }

        public Version UpdateVersion
        {
            get;
            private set;
        }

        private Uri DownloadLink
        {
            get;
            set;
        }

        public UpdateKind Kind
        {
            get;
            private set;
        }

        public string UpdaterArguments
        {
            get;
            private set;
        }

        public string UpdateFileLocalPath
        {
            get;
            private set;
        }

        public bool IsRestartRequired
        {
            get;
            private set;
        }

        public bool IsNewerThanCurrentVersion => IsNewerThan(this.CurrentVersion);

        [NonSerialized]
        private IPathProvider paths;

        public ApplicationUpdate(DateTime datePublished, string title, string htmlBody, Version currentVersion, Version updateVersion, Uri downloadLink, UpdateKind kind, string updaterArguments, bool isRestartRequired)
        {
            paths = PlatformTypes.New<IPathProvider>();

            DatePublished = datePublished;
            Title = title = title != null ? title : string.Empty;
            HtmlBody = htmlBody = htmlBody != null ? htmlBody : string.Empty;
            CurrentVersion = currentVersion;
            UpdateVersion = updateVersion;
            DownloadLink = downloadLink;
            Kind = kind;
            UpdaterArguments = updaterArguments != null ? updaterArguments : string.Empty;
            IsRestartRequired = isRestartRequired;

            var targetDir = Path.Combine(paths.ApplicationDataFolder, "updates");

            if(!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            // XXX TODO - Problems on non-windows plats? Do we care?
            UpdateFileLocalPath = Path.Combine(targetDir, Path.GetFileName(DownloadLink.LocalPath));
        }

        public Task<bool> DownloadUpdate(DownloadProgressChangedEventHandler eventHandler)
        {
            if(File.Exists(UpdateFileLocalPath))
            {
                File.Delete(UpdateFileLocalPath);
            }

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            using(var cli = new WebClient())
            {
                if(eventHandler != null)
                {
                    cli.DownloadProgressChanged += eventHandler;
                    cli.DownloadFileCompleted += (sender, e) =>
                    {

                        if (e.Cancelled || e.Error != null)
                        {
                            tcs.SetResult(false);
                        }
                        else
                        {
                            tcs.SetResult(true);
                        }
                    };
                }

                cli.DownloadFileTaskAsync(DownloadLink, UpdateFileLocalPath);
            }

            return tcs.Task;
        }

        public bool IsNewerThan(Version v)
        {
            return UpdateVersion > v;
        }
       
        /// <summary>
        /// Begins the external installation after a N second delay specified. 
        /// </summary>
        /// <param name="secondDelay">
        /// The number of seconds to wait before starting the actual update.
        /// </param>
        /// <exception cref="Exception">
        /// If the file designated at UpdateFileLocalPath does not exist at the time of this call,
        /// this method will throw.
        /// </exception>
        public void BeginInstallUpdate(bool restartApplication = true) =>
            PlatformTypes.New<IFilterUpdater>().BeginInstallUpdate(this, restartApplication);
    }
}
