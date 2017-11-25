/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;
using System.Runtime.InteropServices;

namespace Citadel.Core.WinAPI
{
    public static class SFC
    {
        [DllImport("sfc.dll")]
        public static extern int SfcIsFileProtected(IntPtr shouldBeNull, [MarshalAs(UnmanagedType.LPWStr)]string filePath);
    }
}