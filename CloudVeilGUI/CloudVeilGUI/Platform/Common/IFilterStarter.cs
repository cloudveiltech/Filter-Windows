using System;
using System.Collections.Generic;
using System.Text;

namespace CloudVeilGUI.Platform.Common
{
    public interface IFilterStarter
    {
        /// <summary>
        /// Starts the filter service. The filter itself will make sure it's running as a background service.
        /// </summary>
        void StartFilter();
    }
}
