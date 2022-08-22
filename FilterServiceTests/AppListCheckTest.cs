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
    }
}
