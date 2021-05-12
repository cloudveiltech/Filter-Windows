/*
* Copyright © 2017-2018 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Filter.Platform.Common.Extensions;
using Filter.Platform.Common.Util;
using System;
using System.Security;

namespace CloudVeil.IPC.Messages
{
    /// <summary>
    /// Enum of the types of authentication actions that an authentication message can represent. 
    /// </summary>
    [Serializable]
    public enum AuthenticationAction
    {
        /// <summary>
        /// Means that the message represents a client requesting that the service attempt
        /// authorization with the embedded credentials. Credentials must be populated within the message.
        /// </summary>
        RequestedWithPassword,

        /// <summary>
        /// Means that the message represents a client requesting that the service attempt
        /// authorization with the embedded credentials. Credentials must be populated within the message.
        /// </summary>
        RequestedWithEmail,

        /// <summary>
        /// Means that the message represents the server notifying the client that they must attempt
        /// to authenticate. The server is asking the client to fetch credentials from the user and
        /// send them to the server so that the server can attempt to perform upstream authentication
        /// with the remote service provider.
        /// </summary>
        Required,

        /// <summary>
        /// Means that the message is simply informational, and is the server notifying the client
        /// that authentication is not possible or failed due to an inability to establish an
        /// internet connection with the remote service provider.
        /// </summary>
        ErrorNoInternet,

        /// <summary>
        /// Means that the message is simply informational, and is the server notifying the client
        /// that authentication is not possible or failed due to an unspecified error.
        /// </summary>
        ErrorUnknown,

        /// <summary>
        /// Means that the message is simply informational, and is the server notifying the client
        /// that a previous attempt to authenticate failed due to the supplied credentials being
        /// rejected by the remote upstream service provider.
        /// </summary>
        Denied,

        /// <summary>
        /// Means that the message is simply informational, and is the server notifying the client
        /// that the previous attempt to authenticate included credential data that was not valid
        /// enough to even attempt authentication. This can be null input, improperly formatted
        /// strings etc. This shouldn't happen, but JUST in case, we can notify specifically
        /// that it did happen.
        /// </summary>
        InvalidInput,

        /// <summary>
        /// Means that the server is notifying the client that they are successfully authenticated.
        /// </summary>
        Authenticated
    }

    /// <summary>
    /// The AuthenticationMessage class represents an IPC communication between client (GUI) and
    /// server (Service) about authenticating with the remote service provider, who provides data to
    /// the service needed to apply filtering.
    /// </summary>
    [Serializable]
    public class AuthenticationMessage : BaseMessage
    {
        /// <summary>
        /// The Action that this message represents. 
        /// </summary>
        public AuthenticationAction Action
        {
            get;
            private set;
        }

        /// <summary>
        /// The authentication result from the web server.
        /// 
        /// This property may sometimes be null, for instance when the service is passing an AuthenticationAction.Required without a failed login first.
        /// </summary>
        public AuthenticationResultObject AuthenticationResult
        {
            get;
            private set;
        }

        /// <summary>
        /// The username to use. This should only be populated whenever the client is constructing
        /// this message with the action specified to Requested, since the client is requesitng that
        /// the server attempt authentication with the given credentials.
        /// </summary>
        public string Username
        {
            get;
            private set;
        } = string.Empty;

        /// <summary>
        /// The password to use. This should only be populated whenever the client is constructing
        /// this message with the action specified to Requested, since the client is requesitng that
        /// the server attempt authentication with the given credentials.
        /// </summary>
        public byte[] Password
        {
            get;
            private set;
        } = new byte[0];

        /// <summary>
        /// Constructs a new AuthenticationMessage instance. 
        /// </summary>
        /// <param name="action">
        /// The action directive of this message. 
        /// </param>
        /// <param name="username">
        /// The username to use. This should only be populated whenever the client is constructing
        /// this message with the action specified to Requested, since the client is requesitng that
        /// the server attempt authentication with the given credentials.
        /// </param>
        /// <param name="password">
        /// The password to use. This should only be populated whenever the client is constructing
        /// this message with the action specified to Requested, since the client is requesitng that
        /// the server attempt authentication with the given credentials.
        /// </param>
        public AuthenticationMessage(AuthenticationAction action, string username = null, SecureString password = null)
        {
            Action = action;
            Username = username != null ? username : string.Empty;
            Password = password != null ? password.SecureStringBytes() : new byte[0];
        }

        /// <summary>
        /// Constructs a new AuthenticationMessage instance. Used by the IPC server to return more detailed information about the 
        /// </summary>
        /// <param name="action">
        /// The action directive of this message.
        /// </param>
        /// <param name="authenticationResult">
        /// The authentication result returned by the web server to the service.
        /// </param>
        public AuthenticationMessage(AuthenticationAction action, AuthenticationResultObject authenticationResult, string username = null)
        {
            Action = action;
            Username = username;
            Password = new byte[0];
            AuthenticationResult = authenticationResult;
        }
    }
}