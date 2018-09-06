using System;
using System.Collections.Generic;
using System.Text;

namespace CloudVeilGUI.Models
{
    public enum MenuItemType
    {
        BlockedPages,
        SelfModeration,
        TimeRestrictions,
        RelaxedPolicy,
        Advanced,
        Support,
        Diagnostics
    }

    public class HomeMenuItem
    {
        public MenuItemType Id { get; set; }

        public string Title { get; set; }
    }
}
