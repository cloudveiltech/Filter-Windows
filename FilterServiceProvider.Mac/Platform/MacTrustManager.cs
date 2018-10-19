// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
using System;
using System.Runtime.InteropServices;
using Filter.Platform.Common.Util;
using FilterProvider.Common.Platform;

namespace FilterServiceProvider.Mac.Platform
{
    public class MacTrustManager : IPlatformTrust
    {
        [DllImport(Filter.Platform.Mac.Platform.NativeLib, EntryPoint = "AddToKeychain")]
        public static extern IntPtr AddToKeychain(byte[] arr, int length, string label);

        [DllImport(Filter.Platform.Mac.Platform.NativeLib, EntryPoint = "GetFromKeychain")]
        public static extern IntPtr GetFromKeychain(string label);

        [DllImport(Filter.Platform.Mac.Platform.NativeLib, EntryPoint = "RemoveFromKeychain")]
        public static extern bool RemoveFromKeychain(IntPtr certificate);

        [DllImport(Filter.Platform.Mac.Platform.NativeLib, EntryPoint = "EnsureCertificateTrust")]
        public static extern void EnsureCertificateTrust(IntPtr certificate);

        [DllImport(Filter.Platform.Mac.Platform.NativeLib, EntryPoint = "GetCertificateBytes")]
        private static extern IntPtr _GetAppleCertificateBytes(IntPtr certificate, out int length);

        /// <summary>
        /// We're using this CFRelease wrapper for releasing our certificate resource. Please do not call this function with non-CF types, or you will get a segmentation fault.
        /// </summary>
        /// <param name="certificate">Certificate.</param>
        [DllImport(Filter.Platform.Mac.Platform.NativeLib, EntryPoint = "__CFRelease")]
        private static extern void CFRelease(IntPtr certificate);

        // This is done because we return a non-GC allocated buffer from GetCertificateBytes() and need a way to release that buffer once we're done marshalling to a C# type.
        [DllImport(Filter.Platform.Mac.Platform.NativeLib, EntryPoint = "__CFDeallocate")]
        private static extern void Deallocate(IntPtr buf);

        private static NLog.Logger s_logger;

        static MacTrustManager()
        {
            s_logger = LoggerUtil.GetAppWideLogger();
        }

        /// <summary>
        /// Use this ONLY TO RELEASE Core Foundation types (such as SecCertificateRefs)
        /// This will crash if you try to deallocate a buffer with it!
        /// </summary>
        /// <param name="certificate">Certificate.</param>
        public static void ReleaseCertificate(IntPtr certificate)
        {
            if(certificate != IntPtr.Zero)
            {
                CFRelease(certificate);
            }
        }

        public static byte[] GetAppleCertificateBytes(IntPtr certificate)
        {
            int len = 0;
            IntPtr buf = _GetAppleCertificateBytes(certificate, out len);

            if(buf == IntPtr.Zero)
            {
                return null;
            }

            byte[] certBytes = new byte[len];

            Marshal.Copy(buf, certBytes, 0, len);
            Deallocate(buf);

            return certBytes;
        }

        public MacTrustManager()
        {
        }

        public void EstablishTrust()
        {
            // No trust needs to be established at this point?
        }
    }
}
