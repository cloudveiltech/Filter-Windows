/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;

namespace Citadel.IPC.Messages
{
    /// <summary>
    /// The ServerUpdateQueryMessage class represents an IPC communication between server (Service)
    /// and client (GUI) about application updates. This message is sent exclusively by the server
    /// with information about an available update. The update in question will only be installed if
    /// a response is received from the client accepting the update in question.
    /// </summary>
    [Serializable]
    public class ServerUpdateQueryMessage : ServerOnlyMessage
    {
        /// <summary>
        /// The update title. 
        /// </summary>
        public string Title
        {
            get;
            private set;
        }

        /// <summary>
        /// The update HTML body. This will usually be release notes in HTML format. 
        /// </summary>
        public string HtmlBody
        {
            get;
            private set;
        }

        /// <summary>
        /// The current application version string. 
        /// </summary>
        public string CurrentVersionString
        {
            get;
            private set;
        }

        /// <summary>
        /// The application version post-update, if accepted. 
        /// </summary>
        public string NewVersionString
        {
            get;
            private set;
        }

        public bool IsRestartRequired
        {
            get;
            private set;
        }

        /// <summary>
        /// Constructs a new ServerUpdateQueryMessage instance. 
        /// </summary>
        /// <param name="title">
        /// The update title. 
        /// </param>
        /// <param name="htmlBody">
        /// The update HTML body. This will usually be release notes in HTML format. 
        /// </param>
        /// <param name="currentVersionString">
        /// The current application version string. 
        /// </param>
        /// <param name="newVersionString">
        /// The application version post-update, if accepted. 
        /// </param>
        public ServerUpdateQueryMessage(string title, string htmlBody, string currentVersionString, string newVersionString, bool isRestartRequired)
        {
            Title = title;
            HtmlBody = htmlBody;
            CurrentVersionString = currentVersionString;
            NewVersionString = newVersionString;
            IsRestartRequired = isRestartRequired;
        }
    }
}