/*
* Copyright © 2017 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CloudVeil.Core.Windows.Util;
using Filter.Platform.Common.Util;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using NLog;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Te.Citadel.UI.Windows;
using Te.Citadel.Util;

namespace Te.Citadel.UI.Views
{
    public partial class BaseView : UserControl
    {
        /// <summary>
        /// Expose the application logging system to each view out of the box.
        /// </summary>
        protected readonly Logger m_logger;

        /// <summary>
        /// Keeps a reference to the parent window even when our view is pulled from the display
        /// list. Allows posting of window modals and such without being currently in view.
        /// </summary>
        private MetroWindow m_parentWindow = null;

        public virtual ScrollBarVisibility ShouldViewScroll => ScrollBarVisibility.Auto;

        /// <summary>
        /// Default constructor. Initializes internal application logging references.
        /// </summary>
        public BaseView()
        {
            m_logger = LoggerUtil.GetAppWideLogger();
        }

        protected override void OnVisualParentChanged(DependencyObject oldParent)
        {
            base.OnVisualParentChanged(oldParent);

            if(this.Parent != null)
            {
                m_parentWindow = this.TryFindParent<BaseWindow>();
            }
        }

        /// <summary>
        /// Via Mahapps windows dialogs, present feedback to the user that the use can only accept.
        /// </summary>
        /// <param name="title">
        /// The large title to display to the user in the dialog.
        /// </param>
        /// <param name="message">
        /// The message content to display to the user in the dialog.
        /// </param>
        /// <param name="acceptButtonText">
        /// The text to display in the acceptance/acknowledgement button.
        /// </param>
        protected async Task<MessageDialogResult> DisplayDialogToUser(string title, string message, string acceptButtonText = "Ok")
        {
            MetroDialogSettings mds = new MetroDialogSettings();
            mds.AffirmativeButtonText = acceptButtonText;

            if(m_parentWindow == null)
            {
                m_parentWindow = this.TryFindParent<BaseWindow>();
            }

            if(m_parentWindow != null)
            {
                var result = await DialogManager.ShowMessageAsync(m_parentWindow, title, message, MessageDialogStyle.Affirmative, mds);
                return result;
            }
            else
            {
                Debug.WriteLine("In BaseView.DisplayDialogToUser(...) - Could not find parent window.");
                m_logger.Error("In BaseView.DisplayDialogToUser(...) - Could not find parent window.");
                return MessageDialogResult.Affirmative;
            }
        }

        /// <summary>
        /// Via Mahapps windows dialogs, asks the user a yes or no question.
        /// </summary>
        /// <param name="title">
        /// The large title to display to the user in the dialog.
        /// </param>
        /// <param name="message">
        /// The message content to display to the user in the dialog.
        /// </param>
        /// <param name="acceptButtonText">
        /// The text to display in the acceptance/acknowledgement button.
        /// </param>
        /// <returns>
        /// The user's response.
        /// </returns>
        protected async Task<MessageDialogResult> AskUserYesNoQuestion(string title, string question, string acceptButtonText = "Yes", string noButtonText = "No")
        {
            MetroDialogSettings mds = new MetroDialogSettings();
            mds.AffirmativeButtonText = acceptButtonText;
            mds.NegativeButtonText = noButtonText;

            if(m_parentWindow == null)
            {
                m_parentWindow = this.TryFindParent<BaseWindow>();
            }

            if(m_parentWindow != null)
            {
                var result = await DialogManager.ShowMessageAsync(m_parentWindow, title, question, MessageDialogStyle.AffirmativeAndNegative, mds);
                return result;
            }
            else
            {
                Debug.WriteLine("In BaseView.DisplayDialogToUser(...) - Could not find parent window.");
                m_logger.Error("In BaseView.DisplayDialogToUser(...) - Could not find parent window.");
                return MessageDialogResult.Affirmative;
            }
        }
    }
}