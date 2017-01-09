using NLog;
using System;
using System.Diagnostics;

namespace Te.Citadel.Util
{
    internal static class LoggerUtil
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
                logger.Error(e.Message);
                logger.Error(e.StackTrace);

                Debug.WriteLine(e.Message);
                Debug.WriteLine(e.StackTrace);

                e = e.InnerException;
            }
        }
    }
}