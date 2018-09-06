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

        public string UserTimezone { get; set; }

        public ObservableCollection<TimeZoneViewModel> TimeZones { get; set; }

        public TimeRestrictionsViewModel()
        {
            TimeZones = new ObservableCollection<TimeZoneViewModel>();

            foreach(var zone in TzdbDateTimeZoneSource.Default.ZoneLocations)
            {
                var timeZone = new TimeZoneViewModel();
                timeZone.ZoneId = zone.ZoneId;
                timeZone.CountryCode = zone.CountryCode;

                string friendlyName;
                if (TimeZoneConverter.TZConvert.TryIanaToWindows(zone.ZoneId, out friendlyName))
                {
                    timeZone.FriendlyName = friendlyName;
                    TimeZones.Add(timeZone);
                }
            }
        }
    }
}
