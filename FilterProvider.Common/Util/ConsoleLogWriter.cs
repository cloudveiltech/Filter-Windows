/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Filter.Platform.Common;
using Filter.Platform.Common.Util;
using System;
using System.IO;
using System.Text;

namespace FilterProvider.Common.Util
{
    class ConsoleLogWriter : TextWriter
    {
        public ConsoleLogWriter(string prefix="console") : base()
        {
            pathsProvider = PlatformTypes.New<IPathProvider>();
            this.prefix = prefix;
        }

        public override Encoding Encoding => Encoding.UTF8;

        private IPathProvider pathsProvider;

        private string prefix;

        private StreamWriter writer = null;

        // This is used to help us keep track of what day the stream was opened, so we can keep console output segregated to its own
        // file by date.
        private DateTime openedDate = DateTime.MinValue;

        // This is to help reduce the amount of times we call DateTime.Now. Every thousand characters we check to see if the date is 
        // changed
        private int characterCount = 0;
        public override void Write(char value)
        {
            try
            {
                if (characterCount > 0 && (characterCount % 1000) == 0)
                {
                    if (DateTime.Now.Date > openedDate)
                    {
                        writer.Close();
                        writer = openLogFile();
                        openedDate = DateTime.Now.Date;
                    }
                }

                if (writer == null)
                {
                    writer = openLogFile();
                    openedDate = DateTime.Now.Date;
                }
            }
            catch(Exception ex)
            {
                LoggerUtil.GetAppWideLogger().Error(ex);
            }

            writer.Write(value);
            writer.Flush();
        }

        private StreamWriter openLogFile()
        {
            string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), $"{prefix}-{DateTime.Now.Date.ToString("yyyy-MM-dd")}.log");

            FileStream log = new FileStream(logPath, FileMode.Append);

            return new StreamWriter(log);
        }
    }
}
