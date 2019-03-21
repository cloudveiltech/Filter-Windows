using FilterNativeWindows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Te.Citadel
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
