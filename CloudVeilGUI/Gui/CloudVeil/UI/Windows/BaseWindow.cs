/*
* Copyright © 2017 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Filter.Platform.Common.Types;
using Filter.Platform.Common.Util;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Gui.CloudVeil.Util;

namespace Gui.CloudVeil.UI.Windows
{
    public class BaseWindow : MetroWindow
    {
        public BaseWindow()
        {
            logger = LoggerUtil.GetAppWideLogger();
        }

        public delegate void OnWindowRestoreRequested();

        public event OnWindowRestoreRequested WindowRestoreRequested;

        private NLog.Logger logger;

        /// <summary>
        /// Post a yes no question to the user in a dialogue. Caller must ensure that they're calling
        /// from within the UI thread.
        /// </summary>
        /// <param name="title">
        /// The dialogue title.
        /// </param>
        /// <param name="question">
        /// The question body.
        /// </param>
        /// <returns>
        /// True if the user answered with yes, false if the user answered with no.
        /// </returns>
        public async Task<bool> AskUserYesNoQuestion(string title, string question)
        {
            // Check to see if a dialog is currently displaying.
            if (await DialogManager.GetCurrentDialogAsync<BaseMetroDialog>(this) != null)
            {
                return false;
            }

            MetroDialogSettings mds = new MetroDialogSettings();
            mds.AffirmativeButtonText = "Yes";
            mds.NegativeButtonText = "No";

            var task = DialogManager.ShowMessageAsync(this, title, question, MessageDialogStyle.AffirmativeAndNegative, mds);

            var userQueryResult = await task;

            return userQueryResult == MessageDialogResult.Affirmative;
        }

        public async Task<LoginDialogData> PromptUserForPassword(string title, string question)
        {
            if(await DialogManager.GetCurrentDialogAsync<BaseMetroDialog>(this) != null)
            {
                return null;
            }

            LoginDialogSettings settings = new LoginDialogSettings();
            settings.ShouldHideUsername = true;
            settings.NegativeButtonVisibility = Visibility.Visible;
            settings.AffirmativeButtonText = "OK";
            settings.NegativeButtonText = "Cancel";
            settings.DefaultButtonFocus = MessageDialogResult.Affirmative;

            return await DialogManager.ShowLoginAsync(this, title, question, settings);
        }

        public async Task<UpdateDialogResult> AskUserUpdateQuestion(string title, string question)
        {
            if (await DialogManager.GetCurrentDialogAsync<BaseMetroDialog>(this) != null)
            {
                return UpdateDialogResult.FailedOpen;
            }
            MetroDialogSettings settings = new MetroDialogSettings();
            settings.AffirmativeButtonText = "Yes";
            settings.NegativeButtonText = "Remind Me Later";
            //settings.FirstAuxiliaryButtonText = "Skip This Version";
            settings.DefaultButtonFocus = MessageDialogResult.Affirmative;

            var userQueryResult = await DialogManager.ShowMessageAsync(this, title, question, MessageDialogStyle.AffirmativeAndNegative, settings);

            switch (userQueryResult)
            {
                case MessageDialogResult.Affirmative:
                    return UpdateDialogResult.UpdateNow;

                case MessageDialogResult.Negative:
                    return UpdateDialogResult.RemindLater;

                case MessageDialogResult.FirstAuxiliary:
                    return UpdateDialogResult.SkipVersion;
                default:
                    return UpdateDialogResult.FailedOpen;
            }

        }

        public async Task<bool> AskUser(string title, string question)
        {
            if (await DialogManager.GetCurrentDialogAsync<BaseMetroDialog>(this) != null)
            {
                return false;
            }

            MetroDialogSettings settings = new MetroDialogSettings();
            settings.AffirmativeButtonText = "Yes";
            settings.NegativeButtonText = "No";
            settings.DefaultButtonFocus = MessageDialogResult.Affirmative;

            var result = await DialogManager.ShowMessageAsync(this, title, question, MessageDialogStyle.AffirmativeAndNegative, settings);
            if(result == MessageDialogResult.Affirmative)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Displays a message to the user as an overlay, that the user can only accept. Caller must
        /// ensure that they're calling from within the UI thread.
        /// </summary>
        /// <param name="title">
        /// The large title for the message overlay.
        /// </param>
        /// <param name="message">
        /// The message content.
        /// </param>
        /// <param name="acceptButtonText">
        /// The text to display in the acceptance button.
        /// </param>
        public async void ShowUserMessage(string title, string message, string acceptButtonText = "Ok")
        {
            // Check to see if a dialog is currently displaying.
            if (await DialogManager.GetCurrentDialogAsync<BaseMetroDialog>(this) != null)
            {
                return;
            }

            MetroDialogSettings mds = new MetroDialogSettings();
            mds.AffirmativeButtonText = acceptButtonText;

            await DialogManager.ShowMessageAsync(this, title, message, MessageDialogStyle.Affirmative, mds);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            if(hwndSource != null)
            {
                hwndSource.AddHook(WndProc);
            }
        }
        
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {   
            // Show window
            if(msg == 0x0018)
            {   
                switch(wParam.ToInt32())
                {
                    // Handle all show/restore commands, ignore everything else.
                    case 9:
                    case 5:
                    case 3:
                    case 10:
                    case 2:
                    case 7:
                    case 8:
                    case 4:
                    case 1:
                    {
                        WindowRestoreRequested?.Invoke();
                        handled = true;
                    }
                    break;
                }
            }

            return IntPtr.Zero;
        }
    }
}