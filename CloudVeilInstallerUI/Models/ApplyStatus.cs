using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudVeilInstallerUI.Models
{
    public class ApplyStatus
    {
        public const int FAIL_NOACTION_REBOOT = -2147024546;
        public const int FAIL_PIPE_NO_DATA = -2147024664;
        public const int FAIL_GENERIC_ERROR = -2147023293;
        public const uint FAIL_UNSUPPORTED_ARCH = 0x80070661;
    }
}
