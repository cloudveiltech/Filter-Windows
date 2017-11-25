/*
* Copyright © 2017 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Citadel.Core.Windows.Util;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using NLog;
using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Te.Citadel.Util;

namespace Te.Citadel.UI.ViewModels
{
    /// <summary>
    /// Serves as the base class for all ViewModels.
    /// </summary>
    public class BaseCitadelViewModel : ObservableObject
    {
        /// <summary>
        /// View change request.
        /// </summary>
        /// <param name="T">
        /// The type of the View to request.
        /// </param>
        public delegate void ViewChangeRequestCallback(Type T);

        /// <summary>
        /// Callback when a view requests another view.
        /// </summary>
        public ViewChangeRequestCallback ViewChangeRequest;

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
        }

        /// <summary>
        /// Private member for public RequestViewChangeCommand property.
        /// </summary>
        private ICommand m_changeViewCommand;

        /// <summary>
        /// Gets the view change request command that is made available to any UserControl element
        /// from the Application level. Calling this command with the type of a View will cause a
        /// request at the application level to display an instance of the view whos type you
        /// specified.
        /// </summary>
        /// <remarks>
        /// This is marked virtual so that the action can be changed in different implementing view
        /// models. It may be desirable to bundle this into a single composite command that would be
        /// returned from this property instead, for example.
        /// </remarks>
        public virtual ICommand RequestViewChangeCommand
        {
            get
            {
                if(m_changeViewCommand == null)
                {
                    m_changeViewCommand = new RelayCommand<Type>(RequestViewChange);
                }

                return m_changeViewCommand;
            }
        }

        /// <summary>
        /// Makes a request for a different view. The sole argument is the type of view you're
        /// requesting.
        /// </summary>
        /// <param name="viewType">
        /// The type of view to request.
        /// </param>
        /// <remarks>
        /// This is a compromise forced upon us due to the fact that the command system is not always
        /// available to us. In the case where our view has been popped from the visual tree but is
        /// still "alive", we can't use the command system anymore. By having an event where our main
        /// application (or someone else we don't know who) might be listening, we still have a way
        /// to request primary view changes while not currently being attached to the visual tree.
        /// For instance a view might go away for some time, and then want to recall itself when its
        /// own internal state has changed, so it can show something new to the user.
        ///
        /// At the end of the day it was a fight between this and having an ugly static class with
        /// static const commands and or events. I consider the latter worse than having a non-static
        /// system that is 99.99% agnostic to its own workings (except pulling in type names of
        /// Views), and the listeners, if any, are agnostic to any inner workings driving such events
        /// and no data is exchanged (again, with the only exception being types).
        /// </remarks>
        protected void RequestViewChange(Type viewType)
        {
            ViewChangeRequest?.Invoke(viewType);
        }

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