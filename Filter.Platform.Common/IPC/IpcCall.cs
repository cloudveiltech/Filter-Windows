using System;
using System.Collections.Generic;
using System.Text;

namespace CloudVeil.IPC
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
        ShutdownForUpdate,
        ConflictsDetected,
        InstallerDownloadProgress,
        InstallerDownloadFinished,
        InstallerDownloadStarted,
        InternetAccessible,
        AdministratorStart,
        CollectComputerInfo,
        ActivationIdentifier,
        AddCustomTextTrigger,
        DumpSystemEventLog,
        SendEventLog,
        BugReportConfirmationValue,
        PortsValue,
        RandomizePortsValue
    }
}
