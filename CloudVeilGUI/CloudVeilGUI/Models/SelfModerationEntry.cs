using System;
using System.Collections.Generic;
using System.Text;

namespace CloudVeilGUI.Models
{
    /// <summary>
    /// This is a dummy class for now. Someday, we'll move this to Citadel.Core.Windows so that we can use it in both the 
    /// GUI and the service.
    /// </summary>
    public class SelfModerationEntry
    {
        public string Url { get; set; }

        // We don't really need a self-moderation type for block only, but I think it would be wise to use the same 
        // system for per-user whitelists.
        public SelfModerationType SelfModerationType { get; set; }
    }
}
