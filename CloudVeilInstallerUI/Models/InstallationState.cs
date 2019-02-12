using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudVeilInstallerUI.Models
{
    public enum InstallationState
    {
        Initializing,
        Downloading,
        Installing,
        Installed,
        Failed,
        FailedDownloading
    }
}
