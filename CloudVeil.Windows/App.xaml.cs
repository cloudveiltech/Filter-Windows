using CloudVeilGUI.Common;
using CloudVeilGUI.Models;
using CloudVeilGUI.Platform.Common;
using CloudVeilGUI.Platform.Windows;
using Filter.Platform.Common;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace CloudVeil.Windows
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            CloudVeil.Core.Windows.Platform.Init();

            PlatformTypes.Register<IFilterStarter>((arr) => new WindowsFilterStarter());
            PlatformTypes.Register<IGuiServices>((arr) => new WindowsGuiServices());
            PlatformTypes.Register<ITrayIconController>((arr) => new WindowsTrayIconController());

            CommonAppServices.Default.Init();

            CommonAppServices.Default.OnStart();
        }
    }
}
