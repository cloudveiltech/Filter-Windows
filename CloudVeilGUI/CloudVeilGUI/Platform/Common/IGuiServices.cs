using System;
using System.Collections.Generic;
using System.Text;

namespace CloudVeilGUI.Platform.Common
{
    public interface IGuiServices
    {
        /// <summary>
        /// Implement a platform-specific way for the app to bring itself to the front when ordered to.
        /// </summary>
        void BringAppToFront();
    }
}
