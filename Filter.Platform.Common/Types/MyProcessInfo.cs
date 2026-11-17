using System;

namespace Filter.Platform.Common.Types
{
    [Serializable]
    public class MyProcessInfo
    {
        public string Filename { get; set; }
        public string Arguments { get; set; }
    }
}
