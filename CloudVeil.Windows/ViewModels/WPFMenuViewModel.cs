using CloudVeilGUI.ViewModels;
using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudVeil.Windows.ViewModels
{
    public class WPFMenuViewModel
    {
        public WPFMenuViewModel(MenuViewModel model)
        {
            this.model = model;
        }

        private MenuViewModel model;
    }
}
