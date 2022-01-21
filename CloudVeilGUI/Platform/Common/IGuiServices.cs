/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using System;
using System.Collections.Generic;
using System.Text;

namespace CloudVeilGUI.Platform.Common
{
    public interface IGuiServices
    {
        /// <summary>
        /// Implement a platform-specific way for the app to bring itself to the front when ordered to.
        /// </summary>
        void BringAppToFront();

        void ShowLoginScreen();

        void ShowCooldownEnforcementScreen();

        void ShowWaitingScreen();

        void ShowMainScreen();

        void DisplayAlert(string title, string message, string okButton);

        //void ShowCertificateErrorsScreen();
    }
}
