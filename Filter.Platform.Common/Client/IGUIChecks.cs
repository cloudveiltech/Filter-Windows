using System;
using System.Collections.Generic;
using System.Text;

namespace Filter.Platform.Common.Client
{
    public interface IGUIChecks
    {
        /// <summary>
        /// Determines whether the GUI process is in a session that it cannot display a window or receive user input.
        /// </summary>
        /// <returns>true if GUI in isolated session.</returns>
        bool IsInIsolatedSession();

        /// <summary>
        /// If there is a platform-specific way for the app to bring another instance to the front, implement this.
        /// This is an optional implementation as we have a second way to bring up client GUIs if this fails.
        /// </summary>
        void DisplayExistingUI();
    }
}
