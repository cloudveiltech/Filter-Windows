using Filter.Platform.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Te.Citadel.Util;

namespace Citadel.Core.Windows
{
    public class WindowsAntitampering : IAntitampering
    {
        public bool IsProcessProtected => CriticalKernelProcessUtility.IsMyProcessKernelCritical;

        public void DisableProcessProtection()
        {
            CriticalKernelProcessUtility.SetMyProcessAsNonKernelCritical();
        }

        public void EnableProcessProtection()
        {
            CriticalKernelProcessUtility.SetMyProcessAsKernelCritical();
        }
    }
}
