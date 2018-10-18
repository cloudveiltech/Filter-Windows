// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
using System;
using System.Runtime.InteropServices;
using FilterProvider.Common.Platform;

namespace FilterServiceProvider.Mac.Platform
{
    public class MacTrustManager : IPlatformTrust
    {
        [DllImport(Filter.Platform.Mac.Platform.NativeLib, EntryPoint = "AddToKeychain")]
        public static extern IntPtr AddToKeychain(byte[] arr, int length, string label);

        [DllImport(Filter.Platform.Mac.Platform.NativeLib, EntryPoint = "GetFromKeychain")]
        public static extern IntPtr GetFromKeychain(string label);

        [DllImport(Filter.Platform.Mac.Platform.NativeLib, EntryPoint = "EnsureCertificateTrust")]
        public static extern void EnsureCertificateTrust(IntPtr certificate);

        [DllImport(Filter.Platform.Mac.Platform.NativeLib, EntryPoint = "GetCertificateBytes")]
        private static extern IntPtr _GetAppleCertificateBytes(IntPtr certificate, out int length);

        [DllImport(Filter.Platform.Mac.Platform.NativeLib, EntryPoint = "__CFRelease")]
        private static extern void CFRelease(IntPtr certificate);

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

            byte[] certBytes = new byte[len];

            Marshal.Copy(buf, certBytes, 0, len);
            CFRelease(buf);

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
