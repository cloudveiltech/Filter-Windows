/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿// Copied from https://github.com/gaborv/xam-forms-transparent-modal/blob/master/CustomFormElements/ModalPage.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace CloudVeilGUI.CustomFormElements
{
	public class ModalPage : ContentPage
	{
        public Task Close()
        {
            var displayEvent = CloseModalRequested;

            Task completion = null;
            if (displayEvent != null)
            {
                var eventArgs = new CloseModalRequestedEventArgs();
                displayEvent(this, eventArgs);
                completion = eventArgs.ClosingPageTask;
            }

            // If there is no task, just create a new completed one
            return completion ?? Task.FromResult<object>(null);
        }

        public event EventHandler<CloseModalRequestedEventArgs> CloseModalRequested;

        public sealed class CloseModalRequestedEventArgs : EventArgs
        {
            public Task ClosingPageTask { get; set; }
        }
    }
}