/*
* Copyright © 2017 Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

namespace CitadelService.Data.Models
{
    internal class MappedBypassListCategoryModel : MappedFilterListCategoryModel
    {
        /// <summary>
        /// Gets the unique 8 bit unsigned integer that represents the category ID within the
        /// filtering engine, as a whitelist. Bypass filters are loaded as both a whitelist and a
        /// blacklist, and then the whitelist is toggled on and off.
        /// </summary>
        public short CategoryIdAsWhitelist
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the unique category name when acting as a whitelist.
        /// </summary>
        public string CategoryNameAsWhitelist
        {
            get;
            private set;
        }

        /// <summary>
        /// Constructs a new MappedFilterListCategoryModel instance.
        /// </summary>
        /// <param name="categoryId">
        /// The generated category ID.
        /// </param>
        /// <param name="categoryName">
        /// The category name.
        /// </param>
        /// <param name="isBypass">
        /// Whether or not this category is a bypassable category.
        /// </param>
        public MappedBypassListCategoryModel(short categoryId, short categoryIdAsWhitelist, string categoryName, string categoryNameAsWhitelist) : base(categoryId, categoryName)
        {
            CategoryIdAsWhitelist = categoryIdAsWhitelist;
            CategoryNameAsWhitelist = categoryNameAsWhitelist;
        }
    }
}