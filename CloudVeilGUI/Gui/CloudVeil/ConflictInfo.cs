using FilterNativeWindows;
using System;

namespace Gui.CloudVeil
{
    public class ConflictInfo
    {
        public ConflictReason ConflictReason { get; set; }
        public string Header { get; set; }
        public string Message { get; set; }

        public string Link { get; set; }

        public Uri LinkUri => new Uri(Link);
    }
}
