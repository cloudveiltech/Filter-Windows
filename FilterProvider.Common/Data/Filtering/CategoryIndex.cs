﻿/*
* Copyright © 2017-2018 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System.Threading;

namespace FilterProvider.Common.Data.Filtering
{
    public class CategoryIndex
    {
        private bool[] categoryIndex;

        public CategoryIndex(short numCategories)
        {
            categoryIndex = new bool[numCategories];
        }

        public bool GetIsCategoryEnabled(short categoryId)
        {
            Thread.MemoryBarrier();
            return categoryIndex[categoryId];
        }

        public void SetIsCategoryEnabled(short categoryId, bool value)
        {
            Thread.MemoryBarrier();
            categoryIndex[categoryId] = value;
        }

        public void SetAll(bool value)
        {
            var len = categoryIndex.Length;
            for(short i = 0; i < len; ++i)
            {
                SetIsCategoryEnabled(i, value);
            }
        }
    }
}