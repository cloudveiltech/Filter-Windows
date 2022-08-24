﻿/*
* Copyright � 2018 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudVeil.Core.Windows.Util
{
    public class HttpUtils
    {
        private static readonly HashSet<string> validHttpMethods = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // We ignore CONNECT stuff for now.
            //"CONNECT",
            "DELETE",
            "GET",
            "HEAD",
            "OPTIONS",
            "POST",
            "PUT"
        };

        public static bool IsValidHttpMethod(string method)
        {
            return validHttpMethods.Contains(method);
        }
    }
}
