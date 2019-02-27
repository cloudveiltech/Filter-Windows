using System;
using System.Collections.ObjectModel;
using System.Linq;
using Filter.Platform.Common.Data.Models;
using FilterProvider.Common.Util;
using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NodaTime;
using NodaTime.Text;

namespace FilterServiceTests
{
    public class TestTzProvider : IDateTimeZoneProvider
    {
        public DateTimeZone this[string id] => throw new NotImplementedException();

        public string VersionId => throw new NotImplementedException();

        public ReadOnlyCollection<string> Ids => throw new NotImplementedException();

        public DateTimeZone GetSystemDefault()
        {
            return DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/New_York");
        }

        public DateTimeZone GetZoneOrNull(string id)
        {
            throw new NotImplementedException();
        }
    }

    public class TestClock : IClock
    {
        public TestClock(ZonedDateTime time)
        {
            CurrentInstant = time.ToInstant();
        }

        public TestClock(string time, DateTimeZone zone)
        {
            ParseResult<LocalDateTime> result = LocalDateTimePattern.GeneralIso.Parse(time);
            CurrentInstant = zone.AtLeniently(result.Value).ToInstant();
        }

        public Instant CurrentInstant { get; set; }
        public Instant GetCurrentInstant()
        {
            return CurrentInstant;
        }
    }

    [TestClass]
    public class TimeDetectionTest
    {
        private TestClock[] getTestClocks(IDateTimeZoneProvider provider)
        {
            TestClock[] clocks = new TestClock[]
            {
                new TestClock("2019-02-28T00:00:00", provider.GetSystemDefault()),
                new TestClock("2019-02-28T23:59:59", provider.GetSystemDefault()),
                new TestClock("2019-02-28T12:25:30", provider.GetSystemDefault()),
                new TestClock("2019-02-28T22:00:00", provider.GetSystemDefault()),
                new TestClock("2019-03-01T05:00:00", provider.GetSystemDefault()),
                new TestClock("2019-03-01T08:14:59", provider.GetSystemDefault()),
                new TestClock("2019-03-01T08:15:00", provider.GetSystemDefault()),
                new TestClock("2019-03-01T13:00:00", provider.GetSystemDefault()),
                new TestClock("2019-03-01T19:00:00", provider.GetSystemDefault()),
                new TestClock("2019-03-02T05:00:00", provider.GetSystemDefault()),
                new TestClock("2019-03-02T08:14:59", provider.GetSystemDefault()),
                new TestClock("2019-03-02T08:15:00", provider.GetSystemDefault()),
                new TestClock("2019-03-02T13:00:00", provider.GetSystemDefault()),
                new TestClock("2019-03-02T19:00:00", provider.GetSystemDefault()),
                new TestClock("2019-03-03T01:23:00", provider.GetSystemDefault()), // For 2019, 03-03 is daylight saving time.
                new TestClock("2019-03-03T08:14:59", provider.GetSystemDefault()),
                new TestClock("2019-03-03T08:15:00", provider.GetSystemDefault()),
                new TestClock("2019-03-03T13:00:00", provider.GetSystemDefault()),
                new TestClock("2019-03-03T19:00:00", provider.GetSystemDefault()),
                new TestClock("2019-03-04T05:00:00", provider.GetSystemDefault()),
                new TestClock("2019-03-04T08:14:59", provider.GetSystemDefault()),
                new TestClock("2019-03-04T08:15:00", provider.GetSystemDefault()),
                new TestClock("2019-03-04T13:00:00", provider.GetSystemDefault()),
                new TestClock("2019-03-04T19:00:00", provider.GetSystemDefault()),
                new TestClock("2019-03-05T05:00:00", provider.GetSystemDefault()),
                new TestClock("2019-03-05T08:14:59", provider.GetSystemDefault()),
                new TestClock("2019-03-05T08:15:00", provider.GetSystemDefault()),
                new TestClock("2019-03-05T13:00:00", provider.GetSystemDefault()),
                new TestClock("2019-03-05T19:00:00", provider.GetSystemDefault()),
                new TestClock("2019-03-06T05:00:00", provider.GetSystemDefault()),
                new TestClock("2019-03-06T08:14:59", provider.GetSystemDefault()),
                new TestClock("2019-03-06T08:15:00", provider.GetSystemDefault()),
                new TestClock("2019-03-06T13:00:00", provider.GetSystemDefault()),
                new TestClock("2019-03-31T19:00:00", provider.GetSystemDefault()),
            };

            return clocks;
        }

        [TestMethod]
        public void TestIsDateTimeAllowed_RestrictionsDisabled()
        {
            TimeRestrictionModel[] models = new TimeRestrictionModel[] {
                new TimeRestrictionModel()
                {
                    EnabledThrough = new decimal[] { 0, 24 },
                    RestrictionsEnabled = false
                },

                new TimeRestrictionModel()
                {
                    EnabledThrough = new decimal[] { 0, 24 },
                    RestrictionsEnabled = true
                },

                new TimeRestrictionModel()
                {
                    EnabledThrough = new decimal[] { 8, 17 },
                    RestrictionsEnabled = false
                }
            };

            var tzProvider = new TestTzProvider();

            TestClock[] clocks = getTestClocks(tzProvider);
            ZonedDateTime[] dates = clocks.Select(c => new ZonedDateTime(c.CurrentInstant, tzProvider.GetSystemDefault())).ToArray();
            bool[] testResults = new bool[34]
            {
                true, true, true, true,
                true, true, true, true, true,
                true, true, true, true, true,
                true, true, true, true, true,
                true, true, true, true, true,
                true, true, true, true, true,
                true, true, true, true, true
            };

            foreach(var model in models)
            {
                for (int i = 0; i < clocks.Length; i++)
                {
                    TimeDetection detection = new TimeDetection(clocks[i], tzProvider);
                    Assert.AreEqual(testResults[i], detection.IsDateTimeAllowed(dates[i], model), "Unexpected result from IsDateTimeAllowed");
                }
            }
        }

        [TestMethod]
        public void TestIsDateTimeAllowed_RestrictionsEnabled1()
        {
            var model = new TimeRestrictionModel()
            {
                EnabledThrough = new decimal[] { 8, 17 },
                RestrictionsEnabled = true
            };

            var tzProvider = new TestTzProvider();

            TestClock[] clocks = getTestClocks(tzProvider);
            ZonedDateTime[] dates = clocks.Select(c => new ZonedDateTime(c.CurrentInstant, tzProvider.GetSystemDefault())).ToArray();
            bool[] testResults = new bool[34]
            {
                false, false, true, false,
                false, true, true, true, false,
                false, true, true, true, false,
                false, true, true, true, false,
                false, true, true, true, false,
                false, true, true, true, false,
                false, true, true, true, false,
            };

            for (int i = 0; i < clocks.Length; i++)
            {
                TimeDetection detection = new TimeDetection(clocks[i], tzProvider);
                Assert.AreEqual(testResults[i], detection.IsDateTimeAllowed(dates[i], model), "Unexpected result from IsDateTimeAllowed");
            }
        }

        [TestMethod]
        public void TestIsDateTimeAllowed_RestrictionsEnabled2()
        {
            var model = new TimeRestrictionModel()
            {
                EnabledThrough = new decimal[] { 8.25m, 17.5m },
                RestrictionsEnabled = true
            };

            var tzProvider = new TestTzProvider();

            TestClock[] clocks = getTestClocks(tzProvider);
            ZonedDateTime[] dates = clocks.Select(c => new ZonedDateTime(c.CurrentInstant, tzProvider.GetSystemDefault())).ToArray();
            bool[] testResults = new bool[34]
            {
                false, false, true, false,
                false, false, true, true, false,
                false, false, true, true, false,
                false, false, true, true, false,
                false, false, true, true, false,
                false, false, true, true, false,
                false, false, true, true, false,
            };

            for (int i = 0; i < clocks.Length; i++)
            {
                TimeDetection detection = new TimeDetection(clocks[i], tzProvider);
                Assert.AreEqual(testResults[i], detection.IsDateTimeAllowed(dates[i], model), "Unexpected result from IsDateTimeAllowed");
            }
        }

        [TestMethod]
        public void TestGetRealTime()
        {
            TimeDetection detection = new TimeDetection(SystemClock.Instance);

            ZonedDateTime time = detection.GetRealTime();

            Assert.IsNotNull(time, "Time should not be null");
        }
    }
}
