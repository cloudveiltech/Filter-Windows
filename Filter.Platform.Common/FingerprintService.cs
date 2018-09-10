using System;
using System.Collections.Generic;
using System.Text;

namespace Filter.Platform.Common
{
    public static class FingerprintService
    {
        public static IFingerprint Default;
        public static void InitFingerprint(IFingerprint instance)
        {
            Default = instance;
        }
    }
}
