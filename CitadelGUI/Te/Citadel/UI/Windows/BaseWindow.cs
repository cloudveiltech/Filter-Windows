/*
* Copyright © 2017 Jesse Nicholson  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Te.Citadel.Util;

namespace Te.Citadel.UI.Windows
{
    public class BaseWindow : MetroWindow
    {

        public delegate void OnWindowRestoreRequested();

        public event OnWindowRestoreRequested WindowRestoreRequested;

        private ReaderWriterLockSlim m_modalLock = new ReaderWriterLockSlim();

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
            // Why does this work? Because there is only 1 UI thread,
            // and if not, you've got big problems.
            if(m_modalLock.IsWriteLockHeld)
            {
                return false;
            }

            try
            {
                m_modalLock.EnterWriteLock();
                MetroDialogSettings mds = new MetroDialogSettings();
                mds.AffirmativeButtonText = "Yes";
                mds.NegativeButtonText = "No";

                var userQueryResult = await DialogManager.ShowMessageAsync(this, title, question, MessageDialogStyle.AffirmativeAndNegative, mds);

                return userQueryResult == MessageDialogResult.Affirmative;
            }
            finally
            {
                m_modalLock.ExitWriteLock();
            }
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
        public void ShowUserMessage(string title, string message, string acceptButtonText = "Ok")
        {   
            // Why does this work? Because there is only 1 UI thread,
            // and if not, you've got big problems.
            if(m_modalLock.IsWriteLockHeld)
            {
                return;
            }

            try
            {
                m_modalLock.EnterWriteLock();

                MetroDialogSettings mds = new MetroDialogSettings();
                mds.AffirmativeButtonText = acceptButtonText;

                DialogManager.ShowMessageAsync(this, title, message, MessageDialogStyle.Affirmative, mds);
            }
            finally
            {
                m_modalLock.ExitWriteLock();
            }
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