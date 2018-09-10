using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace CloudVeilGUI.Models
{
    public class BlockedPagesModel
    {
        public ObservableCollection<BlockedPageEntry> BlockedPages { get; set; }

        public BlockedPagesModel()
        {
            BlockedPages = new ObservableCollection<BlockedPageEntry>();
        }
    }
}
