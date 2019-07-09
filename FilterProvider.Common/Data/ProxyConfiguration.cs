/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using GoProxyWrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace FilterProvider.Common.Data
{
    public delegate void NewHttpMessageHandler(HttpMessageInfo message);
    public delegate void WholeBodyResponseHandler(HttpMessageInfo message);
    public delegate void BadCertificateHandler(HttpMessageInfo message);

    public class ProxyConfiguration
    {
        public string AuthorityName { get; set; }

        public NewHttpMessageHandler NewHttpMessageHandler { get; set; }

        public WholeBodyResponseHandler HttpMessageWholeBodyInspectionHandler { get; set; }

        public BadCertificateHandler BadCertificateHandler { get; set; }

        public GoproxyWrapper.GoProxy.OnBeforeRequest BeforeRequest { get; set; }
        public GoproxyWrapper.GoProxy.OnBeforeResponse BeforeResponse { get; set; }

        public AdBlockMatcherApi.AdBlockCallbackDelegate Whitelisted { get; set; }
        public AdBlockMatcherApi.AdBlockCallbackDelegate Blacklisted { get; set; }
    }
}
