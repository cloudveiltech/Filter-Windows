/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CloudVeilService.Util;
using DotNet.Globbing;
using FilterServiceTests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FilterServiceTests
{
    [TestClass]
    public class AppListCheckTest
    {
        // TODO: This globRegex behavior should be standardized across all configuration types.
        private Glob globMe(string s)
        {
            return Glob.Parse(s, new GlobOptions() { Evaluation = new EvaluationOptions() { CaseInsensitive = true } });
        }

        private MockPolicyConfiguration getConfig()
        {
            var mock = new MockPolicyConfiguration()
            {
                BlacklistedApplications = new HashSet<string>(new string[]
                {
                    "chrome.exe",
                    "testme.exe",
                    @"Program Files*\java\jre.exe",
                    @"Windows\MicrosoftEdge.exe"
                }),

                BlacklistedApplicationGlobs = new HashSet<Glob>(new Glob[]
                {
                    globMe(@"**\Program Files*\java\jre.exe")
                }),

                WhitelistedApplications = new HashSet<string>(new string[]
                {
                    "dropbox.exe",
                    @"eclipse\java\jre.exe",
                    @"IDrive\backup*.exe"
                }),

                WhitelistedApplicationGlobs = new HashSet<Glob>(new Glob[]
                {
                    globMe(@"**\IDrive\backup*.exe")
                })
            };

            return mock;
        }

        [TestMethod]
        public void TestBlacklistedApplications()
        {
            AppListCheck alc = new AppListCheck(getConfig());

            Func<string, bool> isAppInBlacklist = (path) => alc.IsAppInBlacklist(path, System.IO.Path.GetFileName(path));

            Assert.IsTrue(alc.IsAppInBlacklist(@"C:\Program Files\Google\Chrome\chrome.exe", "chrome.exe"));
            Assert.IsTrue(alc.IsAppInBlacklist(@"C:\Program Files (x86)\Google\Chrome\chrome.exe", "chrome.exe"));

            Assert.IsTrue(alc.IsAppInBlacklist(@"C:\testme.exe", "testme.exe"));

            Assert.IsTrue(alc.IsAppInBlacklist(@"C:\Program Files\java\jre.exe", "jre.exe"));
            Assert.IsFalse(isAppInBlacklist(@"C:\Program Files\eclipse\java\jre.exe"));
            Assert.IsFalse(isAppInBlacklist(@"C:\Apps\java\jre.exe"));

            Assert.IsTrue(isAppInBlacklist(@"C:\Windows\MicrosoftEdge.exe"));
            Assert.IsFalse(isAppInBlacklist(@"C:\Program Files\MicrosoftEdge.exe"));
        }

        [TestMethod]
        public void TestWhitelistedApplications()
        {
            AppListCheck alc = new AppListCheck(getConfig());

            Func<string, bool> isAppInWhitelist = (path) => alc.IsAppInWhitelist(path, System.IO.Path.GetFileName(path));

            Assert.IsTrue(isAppInWhitelist(@"C:\Program Files\Dropbox\dropbox.exe"));
            Assert.IsTrue(isAppInWhitelist(@"C:\Program Files\eclipse\java\jre.exe"));
            Assert.IsTrue(isAppInWhitelist(@"C:\Program Files\IDrive\backup-v300.exe"));

            Assert.IsFalse(isAppInWhitelist(@"C:\Program Files\IDrive\backup\invalid.exe"));
        }
    }
}
