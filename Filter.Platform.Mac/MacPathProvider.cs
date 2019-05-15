// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
using Filter.Platform.Common;
using System.IO;

namespace Filter.Platform.Mac
{
    public class MacPathProvider : IPathProvider
    {
        public MacPathProvider()
        {
        }

        public string ApplicationDataFolder => @"/usr/local/share/cloudveil";

        public string GetPath(params string[] pathParts)
        {
            return Path.Combine(ApplicationDataFolder, Path.Combine(pathParts));
        }
    }
}