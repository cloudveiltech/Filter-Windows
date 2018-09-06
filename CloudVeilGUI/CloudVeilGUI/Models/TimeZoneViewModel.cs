using System;
using System.Collections.Generic;
using System.Text;

namespace CloudVeilGUI.Models
{
    public class TimeZoneViewModel
    {
        public string ZoneId { get; set; }
        public string CountryCode { get; set; }

        public string FriendlyName { get; set; }

        public string DisplayName => $"{FriendlyName} ({ZoneId})";
    }
}
