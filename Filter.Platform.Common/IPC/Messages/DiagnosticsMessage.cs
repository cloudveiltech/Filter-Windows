/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Citadel.IPC.Messages
{
    public enum DiagnosticsVersion
    {
        V1
    }

    public enum DiagnosticsType
    {
        RequestSession, BadSsl
    }

    [Serializable]
    public class DiagnosticsMessage : BaseMessage
    {
        public bool EnableDiagnostics { get; set; }
    }

    /// <summary>
    /// This is basically an identical type to the FilterCore DiagnosticsWebSession type.
    /// We just want to use our own type for this so that we don't have to have separate versions
    /// for different versions of the diagnostics classes.
    /// </summary>
    [Serializable]
    public class DiagnosticsInfoMessage : BaseMessage
    {
        /// <summary>
        /// This is an identifier that tells us which diagnostics API version this message contains.
        /// This helps us maintain compatibility with multiple versions of the diagnostics API.
        /// </summary>
        public DiagnosticsVersion ObjectVersion { get; set; }

        /// <summary>
        /// This is the diagnostics info object provided by the filter.
        /// </summary>
        public object Info { get; set; }
    }

    [Serializable]
    public class DiagnosticsInfoV1
    {
        public DiagnosticsType DiagnosticsType { get; set; }

        public byte[] ClientRequestBody { get; set; }
        public byte[] ServerRequestBody { get; set; }

        public string ClientRequestHeaders { get; set; }
        public string ServerRequestHeaders { get; set; }

        public string ClientRequestUri { get; set; }
        public string ServerRequestUri { get; set; }

        public byte[] ServerResponseBody { get; set; }
        public string ServerResponseHeaders { get; set; }

        public DateTime DateStarted { get; set; }
        public DateTime DateEnded { get; set; }

        public string Host { get; set; }
        public Uri RequestUri { get; set; }

        public int StatusCode { get; set; }
    }
}
