/*
* Copyright © 2017 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CloudVeil.Core.Windows.Util;
using CloudVeil.Windows;
using Filter.Platform.Common.Util;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using NLog;
using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Gui.CloudVeil.Util;

namespace Gui.CloudVeil.UI.ViewModels
{
    /// <summary>
    /// Serves as the base class for all ViewModels.
    /// </summary>
    public class BaseCitadelViewModel : ObservableObject
    {

        /// <summary>
        /// Ask user a question.
        /// </summary>
        /// <param name="title">
        /// The question title.
        /// </param>
        /// <param name="question">
        /// The question.
        /// </param>
        /// <returns>
        /// The binary response.
        /// </returns>
        public delegate Task<bool> UserFeedbackCallback(string title, string question);

        /// <summary>
        /// Callback for when a view wants to ask the user a question in a modal.
        /// </summary>
        public UserFeedbackCallback UserFeedbackRequest;

        /// <summary>
        /// Tell the user something.
        /// </summary>
        /// <param name="title">
        /// The modal title.
        /// </param>
        /// <param name="statement">
        /// The statement
        /// </param>
        public delegate void UserNotificationCallback(string title, string statement);

        /// <summary>
        /// Callback for when a view wants to post a notification to the user in a modal.
        /// </summary>
        public UserNotificationCallback UserNotificationRequest;

        /// <summary>
        /// Logger for views.
        /// </summary>
        protected readonly Logger m_logger;

        /// <summary>
        /// Constructs a new BaseCitadelViewModel instance. 
        /// </summary>        
        public BaseCitadelViewModel()
        {
            m_logger = LoggerUtil.GetAppWideLogger();

            ViewManager = ((CitadelApp)CitadelApp.Current).ViewManager;
        }

        protected ViewManager ViewManager { get; set; }

        protected async Task<bool> AskUserQuestion(string title, string question)
        {
            return await UserFeedbackRequest?.Invoke(title, question);
        }

        protected void PostNotificationToUser(string title, string message)
        {
            UserNotificationRequest?.Invoke(title, message);
        }
    }
}