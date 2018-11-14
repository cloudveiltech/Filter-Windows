using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Te.Citadel.UI.ViewModels;
using Te.Citadel.UI.Views;

namespace Te.Citadel.UI
{
    public class ViewManager : ModelManager
    {
        public void Register<T>(T view, Action<BaseView> viewModelAction)
        {
            if (view is BaseView)
            {
                var context = (view as BaseView).DataContext;

                if(context is BaseCitadelViewModel)
                {
                    viewModelAction?.Invoke(view as BaseView);
                }
            }

            Register(view);
        }
    }
}
