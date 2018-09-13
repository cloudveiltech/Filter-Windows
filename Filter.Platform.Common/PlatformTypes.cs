/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Filter.Platform.Common
{
    public static class PlatformTypes
    {
        private static ConcurrentDictionary<Type, Func<object[], object>> s_typeDictionary;

        static PlatformTypes()
        {
            s_typeDictionary = new ConcurrentDictionary<Type, Func<object[], object>>();
        }

        public static T New<T>(params object[] parameters)
        {
            Type type = typeof(T);

            if (s_typeDictionary.ContainsKey(type))
            {
                return (T)s_typeDictionary[type](parameters);
            }
            else
            {
                throw new TypeAccessException($"No available instantiation for type {type.FullName}");
            }
        }

        public static void Register<T>(Func<object[], object> instantiationFunc)
        {
            Type type = typeof(T);

            s_typeDictionary[type] = instantiationFunc;
        }
    }
}
