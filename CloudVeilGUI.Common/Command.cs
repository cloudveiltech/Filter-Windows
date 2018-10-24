using System;
using System.Collections.Generic;
using System.Text;

namespace CloudVeilGUI
{
    public class Command
    {
        public Command(Action cmd)
        {
            CommandAction = cmd;
        }

        public Action CommandAction { get; set; }
    }
}
