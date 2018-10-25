using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Text;
using System.Threading.Tasks;

namespace CloudVeil.Windows.ViewModels
{
    /// <summary>
    /// Use this if you're too lazy to create boilerplate wrappers for WPF-specific view models.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ProxyViewModel<T> : DynamicObject
    {
        T viewModel;
        Type type;

        public ProxyViewModel(T obj)
        {
            this.viewModel = obj;
            this.type = typeof(T);
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            PropertyInfo p = type.GetProperty(binder.Name);

            if(p != null)
            {
                object o = p.GetValue(viewModel);

                if (o is CloudVeilGUI.Command)
                {
                    result = new WPFCommand(o as CloudVeilGUI.Command);
                }
                else
                {
                    result = o;
                }

                return true;
            }

            result = null;
            return false;
        }
    }
}
