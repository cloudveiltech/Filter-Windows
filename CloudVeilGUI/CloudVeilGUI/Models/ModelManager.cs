using System;
using System.Collections.Generic;
using System.Text;

namespace CloudVeilGUI.Models
{
    public class ModelManager
    {
        private Dictionary<Type, object> models;

        public ModelManager()
        {
            this.models = new Dictionary<Type, object>();
        }

        public void Register<T>(T model)
        {
            models[typeof(T)] = model;
        }

        public T GetModel<T>()
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
