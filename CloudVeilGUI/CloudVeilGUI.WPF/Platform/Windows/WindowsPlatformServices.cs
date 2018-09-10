using Citadel.IPC;
using CloudVeilGUI.Platform.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudVeilGUI.Platform.Windows
{
    public class WindowsPlatformServices : PlatformServices
    {
        public override IFilterStarter CreateFilterStarter()
        {
            return new WindowsFilterStarter();
        }
    }
}
