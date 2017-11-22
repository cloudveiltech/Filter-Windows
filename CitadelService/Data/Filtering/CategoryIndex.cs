/*
* Copyright © 2017 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System.Threading;

namespace CitadelService.Data.Filtering
{
    internal class CategoryIndex
    {
        private bool[] m_categoryIndex;

        public CategoryIndex(short numCategories)
        {
            m_categoryIndex = new bool[numCategories];
        }

        public bool GetIsCategoryEnabled(short categoryId)
        {
            Thread.MemoryBarrier();
            return m_categoryIndex[categoryId];
        }

        public void SetIsCategoryEnabled(short categoryId, bool value)
        {
            Thread.MemoryBarrier();
            m_categoryIndex[categoryId] = value;
        }

        public void SetAll(bool value)
        {
            var len = m_categoryIndex.Length;
            for(short i = 0; i < len; ++i)
            {
                SetIsCategoryEnabled(i, value);
            }
        }
    }
}