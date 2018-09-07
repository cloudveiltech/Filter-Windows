using System;
using System.Collections.Generic;
using System.Text;

namespace CloudVeilGUI.Platform.Common
{
    public interface IFilterStarter
    {
        /// <summary>
        /// Checks to see if the filter service is running. If it is not, run the FilterAgent.{Platform} executable.
        /// </summary>
        void StartFilter();
    }
}
