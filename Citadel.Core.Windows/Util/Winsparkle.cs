/*
* Copyright © 2017 Jesse Nicholson  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System.Runtime.InteropServices;

namespace Citadel.Core.Windows.Util
{
    public class WinSparkle
    {
        /// <summary>
        /// Callback where WinSparkle will request if the application can shutdown to allow an
        /// update.
        /// </summary>
        /// <returns>
        /// Zero if a shutdown is not possible, one if a shutdown is possible.
        /// </returns>
        public delegate int WinSparkleCanShutdownCheckCallback();

        /// <summary>
        /// Callback where WinSparkle is requesting a shutdown of the application to allow for an
        /// update. This will immediately follow a call to the WinSparkleCanShutdownCheckCallback
        /// callback, where the return value was one.
        /// </summary>
        public delegate void WinSparkleRequestShutdownCallback();

        [DllImport("WinSparkle.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "win_sparkle_set_can_shutdown_callback", ExactSpelling = true)]
        public static extern void SetCanShutdownCallback(WinSparkleCanShutdownCheckCallback cb);

        [DllImport("WinSparkle.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "win_sparkle_set_shutdown_request_callback", ExactSpelling = true)]
        public static extern void SetShutdownRequestCallback(WinSparkleRequestShutdownCallback cb);

        [DllImport("WinSparkle.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "win_sparkle_init", ExactSpelling = true)]
        public static extern void Init();

        [DllImport("WinSparkle.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "win_sparkle_cleanup", ExactSpelling = true)]
        public static extern void Cleanup();

        [DllImport("WinSparkle.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "win_sparkle_set_appcast_url", ExactSpelling = true)]
        public static extern void SetAppcastUrl(string url);

        [DllImport("WinSparkle.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, EntryPoint = "win_sparkle_set_app_details", ExactSpelling = true)]
        public static extern void SetAppDetails(string companyName, string appName, string appVersion);

        [DllImport("WinSparkle.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "win_sparkle_set_registry_path", ExactSpelling = true)]
        public static extern void SetRegistryPath(string path);

        [DllImport("WinSparkle.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "win_sparkle_check_update_with_ui", ExactSpelling = true)]
        public static extern void CheckUpdateWithUI();

        [DllImport("WinSparkle.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "win_sparkle_check_update_without_ui", ExactSpelling = true)]
        public static extern void CheckUpdateWithoutUI();
    }
}