/*
* Copyright � 2018 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/
﻿using Filter.Platform.Common;
using System;
using System.IO;

namespace Citadel.Core.Windows.Util
{
    public class WindowsPathProvider : IPathProvider
    {

        string appDataFolder;

        public string ApplicationDataFolder
        {
            get
            {
                if (appDataFolder == null)
                {
                    appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"CloudVeil");
                }

                return appDataFolder;
            }
        }

        public string GetPath(params string[] pathParts)
        {
            return Path.Combine(ApplicationDataFolder, Path.Combine(pathParts));
        }
    }
}
