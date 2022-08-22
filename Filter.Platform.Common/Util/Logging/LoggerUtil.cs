/*
* Copyright © 2017-2018 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CloudVeil;
using NLog;
using System;
using System.Diagnostics;
using Sentry.NLog;
using System.IO;

namespace Filter.Platform.Common.Util
{
    public static class LoggerUtil
    {
        private const string SentryTarget = "sentry";
        public static void InitializeSentryIntegration()
        {
            LogManager.ConfigurationReloaded += LogManager_ConfigurationReloaded;
            addSentryLogging();
        }

        private static void LogManager_ConfigurationReloaded(object sender, NLog.Config.LoggingConfigurationReloadedEventArgs e)
        {
            addSentryLogging();
        }
        private static void addSentryLogging()
        {
            LogManager.Configuration?.AddSentry(null, SentryTarget, o =>
            {
                o.Layout = "${message}";
                o.BreadcrumbLayout = "${logger}: ${message}"; // Optionally specify a separate format for breadcrumbs

                o.MinimumBreadcrumbLevel = LogLevel.Error; // Debug and higher are stored as breadcrumbs (default is Info)
                o.MinimumEventLevel = LogLevel.Error; // Error and higher is sent as event (default is Error)

                o.AttachStacktrace = true;
                o.SendDefaultPii = false; // Send Personal Identifiable information like the username of the user logged in to the device

                o.IncludeEventDataOnBreadcrumbs = true; // Optionally include event properties with breadcrumbs
                o.ShutdownTimeoutSeconds = 5;

                o.AddTag("logger", "${logger}");  // Send the logger name as a tag
            });

            LogManager.Configuration?.AddRuleForAllLevels(SentryTarget, LoggerName);
        }

        /// <summary>
        /// Recursively logs the given exception to the supplied logger. Steps through all inner
        /// exceptions until there are none left, writting the message and stack strace.
        /// </summary>
        /// <param name="logger">
        /// The logger to write to.
        /// </param>
        /// <param name="e">
        /// The exception to log to.
        /// </param>
        /// <remarks>
        /// If any of the parameters supplied are invalid, the application will print this in debug
        /// information and simply exit. This method is meant explicitly to not throw.
        /// </remarks>
        public static void RecursivelyLogException(Logger logger, Exception e)
        {
            if(logger == null || e == null)
            {
                // Let's not throw here, just return.
                Debug.WriteLine(string.Format("In {0}::{1}, parameters are null, returning without operation.", nameof(LoggerUtil), nameof(RecursivelyLogException)));
                return;
            }

            while(e != null)
            {
                logger.Error($"{e.GetType().Name}: {e.Message}");
                logger.Error(e.StackTrace);

                Debug.WriteLine($"{e.GetType().Name}: {e.Message}");
                Debug.WriteLine(e.StackTrace);

                e = e.InnerException;
            }
        }

        public static string LoggerName { get; set; } = "CloudVeil";

        /// <summary>
        /// Gets the application wide logger.
        /// </summary>
        /// <returns>
        /// The configured application wide logger.
        /// </returns>
        /// <remarks>
        /// Note that this should be configured already in Nlog config. This is a convenience
        /// function to avoid re-typing the log name everywhere. It offers no security or guarantee
        /// that the named log here will exist.
        /// </remarks>
        public static Logger GetAppWideLogger()
        {
            return LogManager.GetLogger(LoggerName);
        }

        public static string LogFolderPath
        {
            get
            {
                // Scan all Nlog log targets
                var logDir = string.Empty;

                var targets = NLog.LogManager.Configuration.AllTargets;

                foreach (var target in targets)
                {
                    if (target is NLog.Targets.FileTarget)
                    {
                        var fTarget = (NLog.Targets.FileTarget)target;
                        var logEventInfo = new NLog.LogEventInfo { TimeStamp = DateTime.Now };
                        var fName = fTarget.FileName.Render(logEventInfo);

                        if (!string.IsNullOrEmpty(fName) && !string.IsNullOrWhiteSpace(fName))
                        {
                            logDir = Directory.GetParent(fName).FullName;
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(logDir) || string.IsNullOrWhiteSpace(logDir))
                {
                    // Fallback, just in case.
                    logDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                }
                return logDir;
            }
        }
    }
}