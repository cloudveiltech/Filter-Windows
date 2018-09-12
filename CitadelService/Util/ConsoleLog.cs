/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Citadel.Core.Windows.Util;

namespace CitadelService.Util
{
    public static class ConsoleLog
    {
        private static NLog.Logger s_logger;

        static ConsoleLog()
        {
            s_logger = LoggerUtil.GetAppWideLogger();
        }

        public const int LogRotateDays = 3;
        public static void RotateLogs()
        {
            try
            {
                string logFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CloudVeil", "logs");

                IEnumerable<string> consoleFiles = Directory.EnumerateFiles(logFolder, "console-*");
                DateTime earliestFileDate = DateTime.Now.Date.AddDays(-3);

                foreach (string filePath in consoleFiles)
                {
                    var fileInfo = new FileInfo(filePath);

                    if (fileInfo.CreationTime < earliestFileDate)
                    {
                        fileInfo.Delete();
                    }
                }
            }
            catch(Exception ex)
            {
                s_logger.Error(ex, "Couldn't rotate log files");
            }
        }
    }
}
