/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using System;

namespace Filter.Platform.Common.Util
{
    /// <summary>
    /// Enumeration for summarizing the result of an authentication request. 
    /// </summary>
    public enum AuthenticationResult
    {
        /// <summary>
        /// Indicates that the auth request was a failure. 
        /// </summary>
        Failure,

        /// <summary>
        /// Indicates that the auth request was a success. 
        /// </summary>
        Success,

        /// <summary>
        /// Indicates that, during the auth request, a connection using the provided URI could not be established.
        /// </summary>
        ConnectionFailed
    }

    /// <summary>
    /// This class is now used to propagate the AuthenticationResult along with an error message back to the GUI client.
    /// </summary>
    [Serializable]
    public class AuthenticationResultObject
    {
        public static AuthenticationResultObject FailedResult = new AuthenticationResultObject(AuthenticationResult.Failure, "");
        public AuthenticationResult AuthenticationResult { get; set; }
        public string AuthenticationMessage { get; set; }

        public AuthenticationResultObject()
        {

        }

        public AuthenticationResultObject(AuthenticationResult authenticationResult, string authenticationMessage)
        {
            AuthenticationResult = authenticationResult;
            AuthenticationMessage = authenticationMessage;
        }
    }
}