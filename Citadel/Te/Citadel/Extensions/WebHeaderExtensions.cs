/*
* Copyright © 2017 Jesse Nicholson  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Te.Citadel.Util;

namespace Te.Citadel.Extensions
{
    internal static class WebHeaderExtensions
    {

        /// <summary>
        /// Regex for splitting SetCookie values, because Microsoft is terrible and didn't use a
        /// multimap for HTTP response headers.
        /// </summary>
        private static readonly Regex s_setCookieSplitRegex = new Regex(@",[^\s]+=", RegexOptions.ECMAScript | RegexOptions.Compiled);

        private static readonly Regex s_cookieParserRegex = new Regex(@"^([^;]+); ?expires=([^;]+); ?Max-Age=([^;]+); ?path=([^;]+); ?domain=([^;]+)?;? ?(HttpOnly)?", RegexOptions.ECMAScript | RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Gets all of the Set-Cookie directives in a server response as an actual cookie
        /// collection. This is a painful process because Microsoft apparently didn't know that you
        /// need a multimap for storing HTTP headers, meaning that they apparently didn't know that
        /// the same header key can be used more than once according to even very hold HTTP
        /// specifications.
        /// </summary>
        /// <param name="collection">
        /// </param>
        /// <returns>
        /// A CookieContainer populated with all cookies extracted from the response headers.
        /// </returns>
        public static CookieContainer GetResponseCookiesFromService(this WebHeaderCollection collection)
        {
            var cookiesHeaderVal = collection["Set-Cookie"];

            var cookieContainer = new CookieContainer();

            if(cookiesHeaderVal != null)
            {
                var splitPositions = new List<int>();
                var separateValues = new List<string>();

                foreach(Match m in s_setCookieSplitRegex.Matches(cookiesHeaderVal))
                {
                    splitPositions.Add(m.Index);
                }

                foreach(var splitPos in splitPositions)
                {
                    separateValues.Add(cookiesHeaderVal.Substring(0, splitPos));
                    cookiesHeaderVal = cookiesHeaderVal.Substring(splitPos + 1);
                }

                if(cookiesHeaderVal.Length > 0)
                {
                    separateValues.Add(cookiesHeaderVal);
                }
                
                foreach(var nvp in separateValues)
                {
                    var name = nvp.Substring(0, nvp.IndexOf('='));
                    var value = nvp.Substring(nvp.IndexOf('=') + 1);

                    var parsedCookieMatch = s_cookieParserRegex.Match(value);

                    if(parsedCookieMatch != null)
                    {
                        var cookieValue = parsedCookieMatch.Groups[1].Value;
                        var expiresValue = parsedCookieMatch.Groups[2].Value;
                        var maxAgeValue = parsedCookieMatch.Groups[3].Value;
                        var pathValue = parsedCookieMatch.Groups[4].Value;
                        var domainValue = parsedCookieMatch.Groups[5].Value;

                        if(domainValue == null || domainValue.Length == 0)
                        {
                            domainValue = new Uri(WebServiceUtil.Default.ServiceProviderApiPath).Host;
                        }

                        var cookie = new Cookie(name, cookieValue, pathValue, domainValue);

                        DateTime cookieExpireTime = Convert.ToDateTime(expiresValue);
                        cookie.Expires = cookieExpireTime.ToUniversalTime();

                        if(parsedCookieMatch.Groups.Count >= 7)
                        {
                            cookie.HttpOnly = true;
                        }
                        else
                        {
                            cookie.HttpOnly = false;
                        }

                        cookieContainer.Add(cookie);
                    }
                }
            }

            return cookieContainer;
        }
    }
}
