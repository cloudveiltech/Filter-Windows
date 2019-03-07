using System;
using System.Collections.Generic;
using System.Text;

namespace Citadel.IPC
{
    public enum IpcCall
    {
        AddSelfModeratedSite,
        ConfigurationInfo,
        Deactivate,
        RelaxedPolicy,
        TimeRestrictionsEnabled,
        CheckForUpdates,
        UpdateRequestResult,
        SynchronizeSettings,
        RequestUpdate
    }
}
