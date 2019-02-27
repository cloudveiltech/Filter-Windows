using Filter.Platform.Common.Data.Models;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace FilterProvider.Common.Util
{
    public class ZoneTamperingEventArgs : EventArgs
    {
        public ZoneTamperingEventArgs(DateTimeZone oldZone, DateTimeZone newZone)
        {
            OldZone = oldZone;
            NewZone = newZone;
        }

        public DateTimeZone OldZone { get; set; }
        public DateTimeZone NewZone { get; set; }
    }

    /// <summary>
    /// The goal of this class is to keep track of the real current time, regardless of whatever time the computer says it is.
    /// 
    /// </summary>
    public class TimeDetection
    {
        public enum TamperDetection
        {
            TimeTampering,
            ZoneTampering,
            NoTampering
        }

        /// <summary>
        /// Use local tracking for time detection if Stopwatch uses the performance counter.
        /// </summary>
        public static readonly bool UseLocalTracking = Stopwatch.IsHighResolution;

        private readonly int ServerTimeRefreshMilliseconds;

        private Stopwatch timeSinceLastServerMeasurement;
        private ZonedDateTime? lastServerTime;

        private DateTimeZone lastDetectedZone;

        /// <summary>
        /// The measured difference between server time and local time at last measurement. Use this to detect tampering with time.
        /// </summary>
        private Duration? measuredDifference;

        private object lockObj = new object();

        private IClock clock;

        private IDateTimeZoneProvider tzProvider;

        public TimeDetection(IClock clock) : this(clock, DateTimeZoneProviders.Tzdb) { }

        public TimeDetection(IClock clock, IDateTimeZoneProvider tzProvider)
        {
            ServerTimeRefreshMilliseconds = Stopwatch.IsHighResolution ? (15 * 60 * 1000) : (30 * 1000);
            this.clock = clock;
            this.tzProvider = tzProvider;
        }

        private void fillServerTime()
        {
            lock(lockObj)
            {
                lastServerTime = WebServiceUtil.Default.GetServerTime();
                lastDetectedZone = tzProvider.GetSystemDefault();

                if (lastServerTime != null)
                {
                    lastServerTime = lastServerTime.Value.WithZone(lastDetectedZone);
                }
                else
                {
                    lastServerTime = new ZonedDateTime(clock.GetCurrentInstant(), lastDetectedZone);
                }

                measureDifference();

                if (timeSinceLastServerMeasurement == null)
                {
                    timeSinceLastServerMeasurement = Stopwatch.StartNew();
                }
                else
                {
                    timeSinceLastServerMeasurement.Restart();
                }
            }
        }

        private Instant calculateServerInstant()
        {
            lock (lockObj)
            {
                if (!lastServerTime.HasValue)
                {
                    fillServerTime();

                    if (!lastServerTime.HasValue)
                    {
                        return clock.GetCurrentInstant();
                    }
                }

                return lastServerTime.Value.ToInstant().Plus(Duration.FromMilliseconds(timeSinceLastServerMeasurement?.ElapsedMilliseconds ?? 0));
            }
        }

        private Duration measureDifference()
        {
            Instant now = clock.GetCurrentInstant();
            Instant nowServer = calculateServerInstant();

            return nowServer.Minus(now);
        }

        /// <summary>
        /// This function 
        /// </summary>
        /// <returns></returns>
        private TamperDetection detectTampering()
        {
            lock (lockObj)
            {
                Duration currentDifference = measureDifference();

                if (measuredDifference.HasValue)
                {
                    long totalMillisMeasured, totalMillisCurrent;
                    totalMillisMeasured = (long)measuredDifference.Value.TotalMilliseconds;
                    totalMillisCurrent = (long)currentDifference.TotalMilliseconds;

                    long millisDifference = totalMillisMeasured - totalMillisCurrent;

                    // No normal clock should have a jitter over a thousand milliseconds. If the difference 
                    if (Math.Abs(millisDifference) > 60000)
                    {
                        measuredDifference = currentDifference;
                        return TamperDetection.TimeTampering;
                    }
                }

                var currentZone = tzProvider.GetSystemDefault();
                if (lastDetectedZone.Id != currentZone?.Id)
                {
                    lastDetectedZone = currentZone;
                    return TamperDetection.ZoneTampering;
                }
                else
                {
                    return TamperDetection.NoTampering;
                }
            }
        }

        public event EventHandler<ZoneTamperingEventArgs> ZoneTamperingDetected;

        public ZonedDateTime GetRealTime()
        {
            try
            {
                lock (lockObj)
                {
                    DateTimeZone oldZone = lastDetectedZone; // Save this in a variable in case detectTampering overwrites it.
                    TamperDetection tamperingResult = detectTampering();

                    // If the stopwatch is not high-resolution, we're using DateTime as a back-end.
                    // If DateTime is being used as a backend, we're susceptible to time restrictions bypassing, so
                    // attempt to detect tampering and retrigger fillServerTime() when it's detected.
                    if (!Stopwatch.IsHighResolution && tamperingResult == TamperDetection.TimeTampering)
                    {
                        fillServerTime();
                    }

                    if (tamperingResult == TamperDetection.ZoneTampering)
                    {
                        ZoneTamperingDetected?.Invoke(this, new ZoneTamperingEventArgs(oldZone, lastDetectedZone));
                    }

                    if (timeSinceLastServerMeasurement.ElapsedMilliseconds > ServerTimeRefreshMilliseconds)
                    {
                        fillServerTime();
                    }

                    Instant currentInstant = calculateServerInstant();
                    return new ZonedDateTime(currentInstant, lastDetectedZone ?? tzProvider.GetSystemDefault());
                }
            }
            catch
            {
                return clock.GetCurrentInstant().InUtc();
            }
        }

        public bool IsDateTimeAllowed(ZonedDateTime date, TimeRestrictionModel model)
        {
            if (!model.RestrictionsEnabled) return true;

            LocalDateTime d = date.LocalDateTime;

            LocalDateTime startDateLocal, endDateLocal;

            int hour, minute;
            getHoursMinutes(model.EnabledThrough[0], out hour, out minute);
            if (hour == 24)
            {
                startDateLocal = new LocalDateTime(d.Year, d.Month, d.Day, 23, 59, 59, 999);
            }
            else
            {
                startDateLocal = new LocalDateTime(d.Year, d.Month, d.Day, hour, minute);
            }

            getHoursMinutes(model.EnabledThrough[1], out hour, out minute);
            if (hour == 24)
            {
                endDateLocal = new LocalDateTime(d.Year, d.Month, d.Day, 23, 59, 59, 999);
            }
            else
            {
                endDateLocal = new LocalDateTime(d.Year, d.Month, d.Day, hour, minute);
            }

            return (d.CompareTo(startDateLocal) >= 0 && d.CompareTo(endDateLocal) <= 0);
        }

        private static void getHoursMinutes(decimal hourDecimal, out int hour, out int minute)
        {
            int second = 0;
            getHoursMinutesSeconds(hourDecimal, out hour, out minute, out second);
        }

        private static void getHoursMinutesSeconds(decimal hourDecimal, out int hour, out int minute, out int second)
        {
            decimal hourTruncated = Math.Truncate(hourDecimal);


            decimal secondPart = (hourDecimal - hourTruncated) * 3600; // Here this holds the number of seconds in the hour.
            decimal minutePart = Math.Truncate(secondPart / 60); // Number of minutes in the hour.
            secondPart = secondPart - (minutePart * 60); // Subtract number of seconds in whole minutes and leave the rest for the secondPart.

            minute = (int)minutePart;
            hour = (int)hourTruncated;
            second = (int)secondPart;
        }

        public static TimeSpan GetTimeSpanFromDecimal(decimal hourDecimal)
        {
            int hour = 0, minute = 0, second = 0;
            getHoursMinutesSeconds(hourDecimal, out hour, out minute, out second);

            return new TimeSpan(hour, minute, second);
        }
    }
}
