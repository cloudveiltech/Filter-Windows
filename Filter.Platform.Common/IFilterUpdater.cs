using CloudVeil.Core.Windows.Util.Update;

namespace Filter.Platform.Common
{
    public interface IFilterUpdater
    {
        void BeginInstallUpdate(ApplicationUpdate applicationUpdate, bool restartApplication = true);
    }
}
