using System;
using System.Collections.Generic;
using System.Text;

namespace Filter.Platform.Common
{
    public interface IAntitampering
    {
        bool IsProcessProtected { get; }

        /// <summary>
        /// If a platform has any way to protect our process, implement it here.
        /// </summary>
        void EnableProcessProtection();

        /// <summary>
        /// Should disable our platform-specific kernel process protections.
        /// </summary>
        void DisableProcessProtection();
    }
}
