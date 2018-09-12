/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace CitadelService.Util
{
    public interface ICertificateExemptions
    {
        void TrustCertificate(string host, string certHash);

        void AddExemptionRequest(HttpWebRequest request, X509Certificate certificate);

        bool IsExempted(HttpWebRequest request, X509Certificate certificate);
    }
}
