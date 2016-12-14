using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Te.Citadel.Data.Serialization;

namespace Te.Citadel.Data.Models
{
    /// <summary>
    /// The application config model stores data from the server that dictates how the application
    /// should function. This includes which filtering lists to use, how to apply those filtering
    /// lists, which applications to filter, which protection mechanisms to employ, etc.
    /// </summary>
    class AppConfigModel
    {

        /// <summary>
        /// Relative paths to all bundled files that should be applied to blacklisting functionality.
        /// </summary>
        public HashSet<string> Blacklists
        {
            get;
            set;
        } = new HashSet<string>();

        /// <summary>
        /// Relative paths to all bundled files that should be applied to whitelisting functionality.
        /// </summary>
        public HashSet<string> Whitelists
        {
            get;
            set;
        } = new HashSet<string>();

        /// <summary>
        /// Relative paths to all bundled files that should be applied as a blacklist, with the
        /// option to invert the lists functionality into a whitelist on demand.
        /// </summary>
        public HashSet<string> Bypass
        {
            get;
            set;
        } = new HashSet<string>();

        /// <summary>
        /// List of all executable names that should have their net traffic forced through the
        /// application. Note that this cannot be used in conjunction with the whitelisted
        /// applications property. One or the other must be defined, or none at all.
        ///
        /// If blacklisted applications are specified, then only their traffic will be forced
        /// through, automatically whitelisting all other applications. If whitelisted applications
        /// are specified, then all other applications but those listed will have their traffic sent
        /// through. If neither of these properties are defined, then all traffic will be filtered.
        /// </summary>
        public HashSet<string> BlacklistedApplications
        {
            get;
            set;
        } = new HashSet<string>();

        /// <summary>
        /// List of all executable names that should not have their net traffic forced through the
        /// application. Note that this cannot be used in conjunction with the blacklisted
        /// applications property. One or the other must be defined, or none at all.
        ///
        /// If blacklisted applications are specified, then only their traffic will be forced
        /// through, automatically whitelisting all other applications. If whitelisted applications
        /// are specified, then all other applications but those listed will have their traffic sent
        /// through. If neither of these properties are defined, then all traffic will be filtered.
        /// </summary>
        public HashSet<string> WhitelistedApplications
        {
            get;
            set;
        } = new HashSet<string>();

        /// <summary>
        /// Indicates whether or not the application should employ countermeasures to ensure that it
        /// cannot be terminated.
        /// </summary>
        public bool CannotTerminate
        {
            get;
            set;
        } = false;

        /// <summary>
        /// Indicates whether or not the application should permanently disable the internet whenever
        /// the application closes. This is to prevent the internet from being used in any way when
        /// the application is not running.
        /// </summary>
        public bool BlockInternet
        {
            get;
            set;
        } = false;

        /// <summary>
        /// Indicates whether or not the application should track the number of block events over a
        /// given period of time, and then when that threshold is met or exceeded, to block the
        /// internet entirely.
        /// </summary>
        public bool UseThreshold
        {
            get;
            set;
        } = false;

        /// <summary>
        /// The total number of block events that can take place within the supplied threshold period
        /// before the internet is disabled for the duration specified by the timeout period.
        /// </summary>
        public int ThresholdLimit
        {
            get;
            set;
        } = 0;

        /// <summary>
        /// The period of time, in minutes, over which to track the number of block actions. If the
        /// number of block actions within this period meets or exceeds the specified threshold
        /// limit, the internet should be disabled for the supplied timeout period.
        /// </summary>
        [JsonConverter(typeof(IntToMinutesTimespanConverter))]
        public TimeSpan ThresholdTriggerPeriod
        {
            get;
            set;
        } = Timeout.InfiniteTimeSpan;

        /// <summary>
        /// The period of time, in minutes, for which the internet should be disabled if the block
        /// action threshold is met or exceeded.
        /// </summary>
        [JsonConverter(typeof(IntToMinutesTimespanConverter))]
        public TimeSpan ThresholdTimeoutPeriod
        {
            get;
            set;
        } = Timeout.InfiniteTimeSpan;

        /// <summary>
        /// The number of times the user may request relaxed/alternate filtering in a given day.
        /// </summary>
        public int BypassesPermitted
        {
            get;
            set;
        } = 0;

        /// <summary>
        /// The total amount of time, in minutes, that bypass filteres are in effect when granted.
        /// </summary>
        [JsonConverter(typeof(IntToMinutesTimespanConverter))]
        public TimeSpan BypassDuration
        {
            get;
            set;
        } = Timeout.InfiniteTimeSpan;

        public AppConfigModel()
        {

        }
    }
}
