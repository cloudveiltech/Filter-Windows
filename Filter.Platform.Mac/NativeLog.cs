// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
using System;
using System.Runtime.InteropServices;
using System.Text;
using Filter.Platform.Common.Util;
using NLog;

namespace Filter.Platform.Mac
{
    public delegate void NativeLogHandler(int severity, string msg);

    public static class NativeLog
    {
        [DllImport(Platform.NativeLib)]
        private static extern void SetNativeLogCallback(NativeLogHandler cb);

        private static NLog.Logger s_logger;

        static NativeLog()
        {
            s_logger = LoggerUtil.GetAppWideLogger();

            SetNativeLogCallback(NativeLogHandle);
        }

        public static void Init()
        {

        }

        private enum LogSeverity
        {
            Trace,
            Debug,
            Info,
            Warn,
            Error,
            Critical
        }

        private static void NativeLogHandle(int severity, string msg)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("native:: ");
            builder.Append(msg);

            msg = builder.ToString();

            LogSeverity logSeverity = (LogSeverity)severity;
            switch((LogSeverity)severity)
            {
                case LogSeverity.Trace:
                    s_logger.Trace(msg);
                    break;

                case LogSeverity.Debug:
                    s_logger.Debug(msg);
                    break;

                case LogSeverity.Info:
                    s_logger.Info(msg);
                    break;

                case LogSeverity.Warn:
                    s_logger.Warn(msg);
                    break;

                case LogSeverity.Error:
                    s_logger.Error(msg);
                    break;

                case LogSeverity.Critical:
                    s_logger.Fatal(msg);
                    break;
            }
        }
    }
}
