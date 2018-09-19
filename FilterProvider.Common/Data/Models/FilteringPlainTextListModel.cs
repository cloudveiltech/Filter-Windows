/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

namespace FilterProvider.Common.Data.Models
{
    public enum PlainTextFilteringListType
    {
        /// <summary>
        /// A plain text file where each line contains a domain or URL that should be blacklisted.
        /// </summary>
        Blacklist,

        /// <summary>
        /// A plain text file where each line contains a domain or URL that should be whitelisted.
        /// </summary>
        Whitelist,

        /// <summary>
        /// A plain text file where each line contains a domain or URL that should be blacklisted,
        /// but also should also be capable of being transformed on-demand into a whitelist.
        /// </summary>
        BypassList,

        /// <summary>
        /// A plain text file where each line contains arbitrary text that, if detected within a HTML
        /// text payload, should trigger a block action.
        /// </summary>
        TextTrigger
    }

    /// <summary>
    /// The FilteringPlainTextListModel represents, as generically as possible, a plain text data
    /// file that is intended to be used for content filtering. This plain text file may be a list of
    /// domains, urls, text triggers, or something else.
    ///
    /// This model contains a relative path to the plain text file within a parent zip container. It
    /// also gives an enumeration indicating the type or intent of the text data within the file.
    /// </summary>
    public class FilteringPlainTextListModel
    {
        /// <summary>
        /// The type of plain text list that this is.
        /// </summary>
        public PlainTextFilteringListType ListType
        {
            get;
            set;
        }

        /// <summary>
        /// The relative path to the list file inside the parent zip container.
        /// </summary>
        public string RelativeListPath
        {
            get;
            set;
        }
    }
}