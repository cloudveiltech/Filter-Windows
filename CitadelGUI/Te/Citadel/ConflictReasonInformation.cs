using CloudVeil.Windows;
using FilterNativeWindows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Te.Citadel
{
    public static class ConflictReasonInformation
    {
        static ConflictReasonInformation()
        {
            ConflictReasonMessages = new Dictionary<ConflictReason, string>();
            ConflictReasonMessages.Add(ConflictReason.Avast, "We detected that Avast is running on your computer. This will conflict with CloudVeil and prevent it from working properly.");
            ConflictReasonMessages.Add(ConflictReason.CleanInternet, "We detected that Clean Internet is running on your computer. This will conflict with CloudVeil and prevent it from working properly.");
            ConflictReasonMessages.Add(ConflictReason.Bluecoat, "We detected that the BlueCoat Unified Agent is running on your computer. This will conflict with CloudVeil and prevent it from working properly.");
            ConflictReasonMessages.Add(ConflictReason.McAfee, "We detected that one or more McAfee products are running on your computer. This will conflict with CloudVeil and prevent it from working properly.");
            ConflictReasonMessages.Add(ConflictReason.Eset, "We detected that an ESET product is running on your computer. This will conflict with CloudVeil and prevent it from working properly.");
            ConflictReasonMessages.Add(ConflictReason.AVGEnhancedFirewall, "We detected that AVG Enhanced Firewall is enabled. Please disable it to continue using CloudVeil.");

            ConflictReasonLinks = CompileSecrets.ConflictReasonLinks;

            ConflictReasonHeaders = new Dictionary<ConflictReason, string>();
            ConflictReasonHeaders.Add(ConflictReason.Avast, "Avast");
            ConflictReasonHeaders.Add(ConflictReason.CleanInternet, "Clean Internet");
            ConflictReasonHeaders.Add(ConflictReason.Bluecoat, "Blue Coat");
            ConflictReasonHeaders.Add(ConflictReason.McAfee, "McAfee");
            ConflictReasonHeaders.Add(ConflictReason.Eset, "Eset");
            ConflictReasonHeaders.Add(ConflictReason.AVGEnhancedFirewall, "AVG");
        }

        public static Dictionary<ConflictReason, string> ConflictReasonHeaders;
        public static Dictionary<ConflictReason, string> ConflictReasonMessages;
        public static Dictionary<ConflictReason, string> ConflictReasonLinks;

    }
}
