/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using System.Collections.Generic;

namespace Citadel.Core.Windows.Util
{
    // XXX TODO This is icky. There should be a way for Newtonsoft.Json to convert to PascalCase from snake_case.

    /// <summary>
    /// A C# POCO class for the results array objects from https://manage.cloudveil.org/api/uri/lookup/existing?uri=...
    /// </summary>
    public class UriResult
    {
        public int id { get; set; }
        public string URI { get; set; }
        public string file_path { get; set; }
        public int domain_id { get; set; }
        public int subdomain_id { get; set; }
        public string URI_touched { get; set; }
        public int xref_id { get; set; }
        public int category_id { get; set; }
        public string percent { get; set; }
        public string date_calculated { get; set; }
        public int source_id { get; set; }
        public int source_cat_id { get; set; }
        public int category_status { get; set; }
        public string category { get; set; }
        public string source_name { get; set; }
    }

    /// <summary>
    /// A C# POCO class for the JSON returned from https://manage.cloudveil.org/api/uri/lookup/existing?uri=...
    /// </summary>
    public class UriInfo
    {
        public int existing { get; set; }
        public string status { get; set; }
        public int domain_id { get; set; }
        public List<UriResult> results { get; set; }
        public int uri_id { get; set; }
        public List<object> sql { get; set; }
        public string uri { get; set; }
    }
}