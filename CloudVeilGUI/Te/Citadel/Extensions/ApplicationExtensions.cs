/*
* Copyright © 2017 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CloudVeil.Core.Windows.Util;
using Filter.Platform.Common.Util;
using System.Windows;

namespace Te.Citadel.Extensions
{
    public static class ApplicationExtensions
    {
        public static void Shutdown(this Application app, ExitCodes code)
        {
            app.Shutdown((int)code);
        }
    }
}