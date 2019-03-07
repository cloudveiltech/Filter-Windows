using System;

namespace Filter.Platform.Common.Types
{
    [Serializable]
    public enum UpdateDialogResult
    {
        RemindLater,
        UpdateNow,
        SkipVersion,
        FailedOpen
    }
}
