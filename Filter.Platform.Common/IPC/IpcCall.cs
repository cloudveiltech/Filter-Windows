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
        UpdateResult,
        SynchronizeSettings,
        Update,
        StartUpdater,
        ShutdownForUpdate,
        ConflictsDetected,
        InstallerDownloadProgress,
        InstallerDownloadFinished,
        InstallerDownloadStarted,
        InternetAccessible
    }
}
