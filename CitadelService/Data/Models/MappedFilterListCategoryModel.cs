/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

namespace CitadelService.Data.Models
{
    /// <summary>
    /// This class represents a mapped filtering category. We call it mapped because we map the
    /// unique string name of a category to a unique byte value. The filtering engine has a maximum
    /// number of filtering categories that equals the maximum value of a unsigned 8 bit integer, so
    /// we have a maximum category limit of 255.
    ///
    /// The purpose of this class then is to generate these unique 8 bit category indentifiers so
    /// that they can be understood by both the filtering engine and the implementing client
    /// application.
    /// </summary>
    public class MappedFilterListCategoryModel
    {
        /// <summary>
        /// Gets the unique 8 bit unsigned integer that represents the category ID within the
        /// filtering engine.
        /// </summary>
        public short CategoryId
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the unique category name.
        /// </summary>
        public string CategoryName
        {
            get;
            private set;
        }

        public string ShortCategoryName
        {
            get
            {
                return CategoryName.Trim('/').Split('/')[1];
            }
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
        public MappedFilterListCategoryModel(short categoryId, string categoryName)
        {
            CategoryId = categoryId;
            CategoryName = categoryName;
        }
    }
}