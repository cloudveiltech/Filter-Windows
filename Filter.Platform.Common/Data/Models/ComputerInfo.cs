using System;
namespace Filter.Platform.Common.Data.Models
{
    [Serializable]
    public class EmailAuthResponse
    {
        public const string TWO_FACTOR_TYPE = "2fa";
        public string type { get; set; }
    }
}
