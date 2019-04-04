using System;
using System.Collections.Generic;
using System.Text;

namespace Filter.Platform.Common.Types
{
    [Serializable]
    public class MyProcessInfo
    {
        public string Filename { get; set; }
        public string Arguments { get; set; }
    }
}
