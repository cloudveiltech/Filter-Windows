using Filter.Platform.Common.Types;
using System;
using System.Collections.Generic;
using System.Text;

namespace Filter.Platform.Common.IPC.Messages
{
    [Serializable]
    public class UpdateCheckInfo
    {
        public UpdateCheckInfo(DateTime? lastChecked, UpdateCheckResult? result)
        {
            LastChecked = lastChecked;
            CheckResult = result;
        }

        public DateTime? LastChecked { get; set; }
        public UpdateCheckResult? CheckResult { get; set; }
    }

    [Serializable]
    public class ConfigCheckInfo
    {
        public ConfigCheckInfo(DateTime? lastChecked, ConfigUpdateResult? result)
        {
            LastChecked = lastChecked;
            CheckResult = result;
        }

        public DateTime? LastChecked { get; set; }
        public ConfigUpdateResult? CheckResult { get; set; }
    }

    [Serializable]
    public class BugReportSetting
    {
        public BugReportSetting(bool allowed, bool dialogShown)
        {
            Allowed = allowed;
            DialogShown = dialogShown;
        }

        public bool Allowed { get; set; }
        public bool DialogShown { get; set; }
    }
}
