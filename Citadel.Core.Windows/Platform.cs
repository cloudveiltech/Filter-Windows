using Citadel.Core.Windows.Util;
using CitadelService.Platform;
using Filter.Platform.Common;
using Filter.Platform.Common.IPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Te.Citadel.Platform;

namespace Citadel.Core.Windows
{
    public static class Platform
    {
        public static void Init()
        {
            FingerprintService.InitFingerprint(new WindowsFingerprint());

            // These loosely typed parameter lists are rather gross. Is there a cleaner way to do this?
            // params: channel, autoReconnect
            PlatformTypes.Register<IPipeClient>((arr) => new WindowsPipeClient((string)arr[0], (arr.Length > 1) ? (bool)arr[1] : false));

            // params: channel
            PlatformTypes.Register<IPipeServer>((arr) => new WindowsPipeServer((string)arr[0]));
        }
    }
}
