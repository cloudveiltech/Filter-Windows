using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Citadel.Core.Windows.Types
{
    [Serializable]
    public enum ConfigUpdateResult
    {
        Updated = 1,
        UpToDate = 2,
        NoInternet = 3,
        ErrorOccurred = 4,

        AppUpdateAvailable = 1 << 8
    }
}
