/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using AppKit;
using System;
using System.IO;
using System.Reflection;

namespace CloudVeilGUI.MacOS
{
    static class MainClass
    {
        // Since we're using a mac app bundle, NLog doesn't know where to find
        // its configuration file. Find it for NLog and load it.
        static void LoadNLogConfiguration()
        {
            string currentAssemblyPath = Assembly.GetCallingAssembly().Location;
            string resourcesPath = Path.Combine(Path.GetDirectoryName(currentAssemblyPath), "..", "Resources");
            resourcesPath = Path.GetFullPath(resourcesPath);

            string nlogConfig = Path.Combine(resourcesPath, "NLog.config");

            if (!File.Exists(nlogConfig))
            {
                Console.WriteLine("No NLog.config found.");
            }
            else
            {
                NLog.LogManager.LoadConfiguration(nlogConfig);
            }
        }

        static void Main(string[] args)
        {
            try
            {
                LoadNLogConfiguration();
            }
            catch(Exception ex) { }

            NSApplication.Init();
            NSApplication.SharedApplication.Delegate = new AppDelegate();
            NSApplication.Main(args);
        }
    }
}
