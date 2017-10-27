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
        Updated,
        UpToDate,
        NoInternet,
        ErrorOccurred
    }
}
