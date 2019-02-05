using System;
using System.Text;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Filter.Platform.Common.Data.Models;
using Newtonsoft.Json;
using System.IO;
using FilterProvider.Common.Configuration;
using CitadelService.Util;
using Filter.Platform.Common;
using Moq;

namespace FilterServiceTests
{
    /// <summary>
    /// Summary description for DefaultPolicyConfigurationTest
    /// </summary>
    [TestClass]
    public class DefaultPolicyConfigurationTest
    {
        public DefaultPolicyConfigurationTest()
        {
            //
            // TODO: Add constructor logic here
            //
        }

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        [ClassInitialize()]
        public static void ClassInit(TestContext context)
        {
            var antitampering = new Mock<IAntitampering>();

            PlatformTypes.Register<IPathProvider>((arr) => new Mocks.MockPathProvider());
            PlatformTypes.Register<IAntitampering>((arr) => antitampering.Object);
        }

        private string provideConfigurationJsonForApplicationLists()
        {
            AppConfigModel model = new AppConfigModel();
            model.BlacklistedApplications = new HashSet<string>(new string[]
            {
                "chrome.exe",
                "testme.exe",
                    @"Program Files*\java\jre.exe",
                    @"Windows\MicrosoftEdge.exe"
            });

            model.WhitelistedApplications = new HashSet<string>(new string[]
            {
                "dropbox.exe",
                    @"eclipse\java\jre.exe",
                    @"IDrive\backup*.exe"
            });

            return JsonConvert.SerializeObject(model);
        }

        private DefaultPolicyConfiguration setupConfiguration(bool useInvalidJson = false)
        {
            string configFileText = useInvalidJson ? "404 Some nasty error has occurred" : provideConfigurationJsonForApplicationLists();

            DefaultPolicyConfiguration configuration = new DefaultPolicyConfiguration(null, NLog.LogManager.GetCurrentClassLogger(), new System.Threading.ReaderWriterLockSlim());

            using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(configFileText)))
            {
                Assert.AreEqual(!useInvalidJson, configuration.LoadConfiguration(ms), "LoadConfiguration delivered unexpected result for the circumstances.");
            }

            return configuration;
        }

        [TestMethod]
        public void TestNullBehavior()
        {
            DefaultPolicyConfiguration configuration = setupConfiguration(true);

            AppListCheck alc = new AppListCheck(configuration);

            Func<string, bool> isAppInBlacklist = (path) => alc.IsAppInBlacklist(path, System.IO.Path.GetFileName(path));

            bool blacklistVal = isAppInBlacklist(@"C:\Program Files\Google\Chrome\chrome.exe");
            bool whitelistVal = alc.IsAppInWhitelist(@"C:\Program Files\Google\Chrome\chrome.exe", "chrome.exe");

            // The result doesn't matter, we just don't want any NullReferenceExceptions occurring.
        }

        [TestMethod]
        public void TestApplicationBlacklists()
        {
            DefaultPolicyConfiguration configuration = setupConfiguration();

            AppListCheck alc = new AppListCheck(configuration);

            Func<string, bool> isAppInBlacklist = (path) => alc.IsAppInBlacklist(path, System.IO.Path.GetFileName(path));

            Assert.IsTrue(alc.IsAppInBlacklist(@"C:\Program Files\Google\Chrome\chrome.exe", "chrome.exe"));
            Assert.IsTrue(alc.IsAppInBlacklist(@"C:\Program Files (x86)\Google\Chrome\chrome.exe", "chrome.exe"));

            Assert.IsTrue(alc.IsAppInBlacklist(@"C:\testme.exe", "testme.exe"));

            Assert.IsTrue(alc.IsAppInBlacklist(@"C:\Program Files\java\jre.exe", "jre.exe"));
            Assert.IsFalse(isAppInBlacklist(@"C:\Program Files\eclipse\java\jre.exe"));
            Assert.IsFalse(isAppInBlacklist(@"C:\Apps\java\jre.exe"));

            Assert.IsTrue(isAppInBlacklist(@"C:\Windows\MicrosoftEdge.exe"));
            Assert.IsFalse(isAppInBlacklist(@"C:\Program Files\MicrosoftEdge.exe"));

            Assert.IsTrue(isAppInBlacklist(@"c:\windows\microsoftedge.exe"));
            Assert.IsTrue(isAppInBlacklist(@"C:\WINDOWS\MICROSOFTEDGE.EXE"));
            Assert.IsTrue(isAppInBlacklist(@"C:\PROGRAM FILES\GOOGLE\CHROME\CHROME.EXE"));
            Assert.IsTrue(isAppInBlacklist(@"C:\PROGRAM FILES\JAVA\JRE.EXE"));

            Assert.IsTrue(isAppInBlacklist(@"c:\program files (x86)\java\JRE.EXE"));
        }

        [TestMethod]
        public void TestApplicationWhitelists()
        {
            DefaultPolicyConfiguration configuration = setupConfiguration();

            AppListCheck alc = new AppListCheck(configuration);

            Func<string, bool> isAppInWhitelist = (path) => alc.IsAppInWhitelist(path, System.IO.Path.GetFileName(path));

            Assert.IsTrue(isAppInWhitelist(@"C:\Program Files\Dropbox\dropbox.exe"));
            Assert.IsTrue(isAppInWhitelist(@"C:\Program Files\eclipse\java\jre.exe"));
            Assert.IsTrue(isAppInWhitelist(@"C:\Program Files\IDrive\backup-v300.exe"));

            Assert.IsFalse(isAppInWhitelist(@"C:\Program Files\IDrive\backup\invalid.exe"));

            Assert.IsTrue(isAppInWhitelist(@"d:\program files\dropbox\dropbox.exe"));
            Assert.IsTrue(isAppInWhitelist(@"D:\PROGRAM FILES (X86)\DROPBOX\DROPBOX.EXE"));

            Assert.IsTrue(isAppInWhitelist(@"D:\PROGRAM FILES (X86)\IDRIVE\BACKUP-V301.EXE"));

            Assert.IsFalse(isAppInWhitelist(@"D:\PROGRAM FILES (X86)\IDRIVE\BACKUP-V301.EXE\trick.exe"));

        }
    }
}
