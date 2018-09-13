/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Citadel.IPC.Messages
{
    [Serializable]
    public class BlockActionReviewRequestMessage : ClientOnlyMessage
    {
        public string CategoryName
        {
            get;
            private set;
        }

        public string FullRequestUrl
        {
            get;
            private set;
        }

        public BlockActionReviewRequestMessage(string categoryName, string fullRequestUrl)
        {
            CategoryName = categoryName;
            FullRequestUrl = fullRequestUrl;
        }
    }
}
