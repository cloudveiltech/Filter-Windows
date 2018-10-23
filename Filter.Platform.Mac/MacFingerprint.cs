// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
using System;
using System.Runtime.InteropServices;
using Filter.Platform.Common;

namespace Filter.Platform.Mac
{
    public class MacFingerprint : IFingerprint
    {
        [DllImport("Filter.Platform.Mac.Native")]
        private static extern string GetSystemFingerprint();

        private string _fingerprint = null;
        public string Value
        {
            get
            {
                if(_fingerprint == null)
                {
                    _fingerprint = GetSystemFingerprint();
                }

                return _fingerprint;
            }
        }
    }
}
