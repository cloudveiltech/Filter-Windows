/*
* Copyright © 2018 CloudVeil Technology, Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;
using System.IO;
using System.Diagnostics;

using CloudVeilGUI.Platform.Common;
using Filter.Platform.Common.Util;

namespace CloudVeil.Mac.Platform
{
    public class MacFilterStarter : IFilterStarter
    { 

        const string LaunchDaemonsFolder = "/Library/LaunchDaemons";
        const string SharedCloudVeilFolder = "/usr/local/share/cloudveil";

        NLog.Logger logger;

        public MacFilterStarter()
        {
            logger = LoggerUtil.GetAppWideLogger();
        }

        private bool isFilterRunning()
        {
            string currentPidFile = Path.Combine(SharedCloudVeilFolder, "current.pid");

            if(!File.Exists(currentPidFile))
            {
                return false;
            }
            else
            {
                string pidString = File.ReadAllText(currentPidFile);

                int pid = 0;
                if(!int.TryParse(pidString, out pid))
                {
                    return false;
                }

                // Check running PID.
                Process checkProcess = new Process();
                checkProcess.StartInfo.UseShellExecute = false;
                checkProcess.StartInfo.FileName = "/bin/ps";
                checkProcess.StartInfo.Arguments = $"-p {pid} -o comm";

                checkProcess.Start();

                bool result = false;

                using(var reader = checkProcess.StandardOutput)
                {
                    string line;
                    while((line = reader.ReadLine()) != null)
                    {
                        if(line.Contains("FilterServiceProvider.Mac"))
                        {
                            result = true;
                        }
                    }
                }

                checkProcess.WaitForExit(2);
                return result;
            }
        }

        public void StartFilter()
        {
            // Check to see if our plist is installed in /Library/LaunchDaemons
            // If it is, check to see if FilterServiceProvider.Mac is running. If it isn't, start it.
            // If plist doesn't exist, uh-oh! For now, we need to bootstrap enough that we can send an
            // error into the accountability partner system.

            bool hasError = false;

            if(File.Exists(Path.Combine(LaunchDaemonsFolder, "org.cloudveil.filterserviceprovider.plist")))
            {
                if(!isFilterRunning())
                {
                    Process startFilter = new Process();
                    startFilter.StartInfo.UseShellExecute = false;
                    startFilter.StartInfo.FileName = "/bin/launchctl";
                    startFilter.StartInfo.Arguments = "start org.cloudveil.filterserviceprovider";

                    startFilter.Start();
                    startFilter.WaitForExit(100);

                    if(startFilter.ExitCode != 0)
                    {
                        logger.Error("Failed to start filter.");

                        /*using (var reader = startFilter.StandardError)
                        {
                            logger.Error(reader.ReadToEnd());
                        }*/
                        // TODO: Maybe StandardError works before the process exits?
                    }
                }
            }
            else
            {
                logger.Error("FilterServiceProvider plist does not exist. Please reinstall.");
                // TODO: Notify accountability service, attach this computer's serial.
            }
        }
    }
}
