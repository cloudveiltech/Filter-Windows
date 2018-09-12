/*
* Copyright © 2017-2018 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Citadel.Core.Windows.Util
{
    public class HttpUtils
    {
        private static readonly HashSet<string> s_ValidHttpMethods = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
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
            return s_ValidHttpMethods.Contains(method);
        }
    }
}
