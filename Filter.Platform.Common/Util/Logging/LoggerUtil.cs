/*
* Copyright © 2017-2018 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using NLog;
using System;
using System.Diagnostics;

namespace Filter.Platform.Common.Util
{
    public static class LoggerUtil
    {
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

        public static string LoggerName { get; set; } = "Citadel";

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
    }
}