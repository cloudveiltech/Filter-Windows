using CloudVeilGUI.Models;
using NodaTime.TimeZones;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace CloudVeilGUI.ViewModels
{
    public class TimeRestrictionsViewModel
    {
        public bool IsTimeRestrictionsEnabled { get; set; }
        
        public TimeSpan FromTime { get; set; }
        public TimeSpan ToTime { get; set; }

        public TimeRestrictionsViewModel()
        {

        }
    }
}
