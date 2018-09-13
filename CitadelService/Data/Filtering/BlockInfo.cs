/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using Citadel.IPC.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CitadelService.Data.Filtering
{
    public class BlockInfo
    {
        public BlockInfo(BlockType cause, Uri requestUri, string categoryNameString, string matchingRule)
        {
            this.cause = cause.ToString();
            this.request_uri = requestUri.ToString();
            this.category_name_string = categoryNameString;
            this.matching_rule = matchingRule;
        }

        public string cause { get; set; }
        public string request_uri { get; set; }
        public string category_name_string { get; set; }
        public string matching_rule { get; set; }
    }
}
