using Citadel.Core.Windows.Util;
using Filter.Platform.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Citadel.Core.Windows
{
    public static class Platform
    {
        public static void Init()
        {
            FingerprintService.InitFingerprint(new WindowsFingerprint());
        }
    }
}
