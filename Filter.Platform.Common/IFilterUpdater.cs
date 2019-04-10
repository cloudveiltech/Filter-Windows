using Citadel.Core.Windows.Util.Update;
using System;
using System.Collections.Generic;
using System.Text;

namespace Filter.Platform.Common
{
    public interface IFilterUpdater
    {
        void BeginInstallUpdate(ApplicationUpdate applicationUpdate, bool restartApplication = true);
    }
}
