/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CloudVeilService.Util
{
    public static class ResourceStreams
    {
        public static byte[] Get(string resourceName)
        {
            try
            {
                using (var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    if (resourceStream != null && resourceStream.CanRead)
                    {
                        using (TextReader tsr = new StreamReader(resourceStream))
                        {
                            return Encoding.UTF8.GetBytes(tsr.ReadToEnd());
                        }
                    }
                    else
                    {
                        //logger.Error("Cannot read from packed block page file.");
                        return null;
                    }
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
