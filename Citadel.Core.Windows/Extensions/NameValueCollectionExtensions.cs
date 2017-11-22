/*
* Copyright © 2017 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Citadel.Core.Windows.Util;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Te.Citadel.Util;

namespace Citadel.Core.Extensions
{
    public static class NameValueCollectionExtensions
    {
        public static bool TryGetHostDeclaration(this NameValueCollection collection, out string host)
        {   
            if((host = collection["Host"]) != null)
            {
                return true;
            }

            host = null;
            return false;
        }

        public static bool TryGetHttpVersion(this NameValueCollection collection, out string httpVersion)
        {
            // This may be a mash up of both HTTP request and response headers.
            // We mash these together in our application to make filtering 
            // simpler.
            foreach(string key in collection)
            {
                string val = collection[key];
                if(val == null || val == string.Empty)
                {
                    var si = key.IndexOf(' ');
                    if(si != -1)
                    {
                        var sub = key.Substring(0, si);

                        if(HttpUtils.IsValidHttpMethod(sub))
                        {
                            var split = key.Split(' ');
                            if(split.Length == 3)
                            {
                                httpVersion = split[2].Substring(split[2].LastIndexOf('/') + 1);
                                httpVersion = split[2].StartsWith("HTTP/") ? httpVersion : "1.1";
                                return true;
                            }
                        }

                        if(sub.StartsWith("HTTP/"))
                        {
                            httpVersion = sub.Substring(5);
                            return true;
                        }
                    }
                }
            }

            httpVersion = null;
            return false;
        }

        public static bool TryGetRequestUri(this NameValueCollection collection, out Uri result)
        {
            string host = null;
            if((host = collection["Host"]) != null)
            {
                // Build out the request URI.
                foreach(string key in collection)
                {
                    string val = collection[key];
                    if(val == null || val == string.Empty)
                    {
                        var si = key.IndexOf(' ');
                        if(si != -1)
                        {
                            var sub = key.Substring(0, si);

                            if(HttpUtils.IsValidHttpMethod(sub))
                            {
                                var finalUri = key.Substring(si + 1);
                                finalUri = finalUri.Substring(0, finalUri.LastIndexOf(' '));
                                result = new Uri(string.Format("http://{0}{1}", host, finalUri));
                                return true;
                            }
                        }
                    }
                }
            }

            result = null;
            return false;
        }       
    }
}
