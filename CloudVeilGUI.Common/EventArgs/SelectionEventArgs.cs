using System;
using System.Collections.Generic;
using System.Text;

namespace CloudVeilGUI.Common
{
    public class SelectionEventArgs : EventArgs
    {
        public SelectionEventArgs(object selectedItem)
        {
            SelectedItem = selectedItem;
        }

        public object SelectedItem { get; private set; }
    }
}
