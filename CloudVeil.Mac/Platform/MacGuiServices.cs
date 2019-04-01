//
// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//

using System;
using AppKit;
using CloudVeilGUI.Platform.Common;

namespace CloudVeil.Mac.Platform
{
    public class MacGuiServices : IGuiServices
    {
        public MacGuiServices()
        {
        }

        public static MainViewController MainViewController { get; set; }

        public void BringAppToFront()
        {
            NSApplication.SharedApplication.ActivateIgnoringOtherApps(true);
        }

        public void DisplayAlert(string title, string message, string okButton)
        {
            Console.WriteLine("DisplayAlert() not implemented");
            //throw new NotImplementedException();
        }

        public void ShowCooldownEnforcementScreen()
        {
            Console.WriteLine("ShowCooldownEnforcementScreen() not implemented");
        }

        public void ShowLoginScreen()
        {
            MainViewController.ShowLoginView();
        }

        public void ShowMainScreen()
        {
            MainViewController.DismissAllModals();
        }

        public void ShowWaitingScreen()
        {
            MainViewController.ShowWaitingView();
        }
    }
}
