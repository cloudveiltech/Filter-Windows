/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Text;

namespace FilterProvider.Common.Data
{
    /// <summary>
    /// Generalized HttpMessageInfo for FilterProvider.Common.
    /// Allows us to use different proxy implementations on different platforms.
    /// </summary>
    public class HttpMessageInfo
    {
        public ProxyNextAction ProxyNextAction { get; set; }

        public Uri Url { get; set; }

        public MessageType MessageType { get; set; }

        public NameValueCollection Headers { get; set; }

        public HttpStatusCode StatusCode { get; set; }

        public string ContentType { get; set; }

        public Memory<byte> Body { get; set; }

        public void Make204NoContent()
        {
            Body = null;
            StatusCode = HttpStatusCode.NoContent;
        }

        public void CopyAndSetBody(byte[] body, int start, int length, string contentType)
        {
            if(body == null)
            {
                return;
            }

            ContentType = contentType;

            byte[] newArray = new byte[length];
            Buffer.BlockCopy(body, start, newArray, 0, length);

            Body = new Memory<byte>(newArray);

            if(StatusCode == HttpStatusCode.NoContent)
            {
                StatusCode = HttpStatusCode.OK;
            }
        }
    }
}
