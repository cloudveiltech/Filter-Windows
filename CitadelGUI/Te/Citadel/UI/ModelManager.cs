/*
* Copyright © 2018 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Te.Citadel.UI
{
    public class ModelManager
    {
        private Dictionary<Type, object> models;

        public ModelManager()
        {
            this.models = new Dictionary<Type, object>();
        }

        public virtual void Register<T>(T model)
        {
            models[typeof(T)] = model;
        }

        public T Get<T>()
        {
            object model;

            if (models.TryGetValue(typeof(T), out model))
            {
                return (T)model;
            }
            else
            {
                return default(T);
            }
        }
    }
}
