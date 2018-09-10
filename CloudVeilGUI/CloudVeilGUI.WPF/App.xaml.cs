using CloudVeilGUI.Platform.Common;
using CloudVeilGUI.Platform.Windows;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace CloudVeilGUI.WPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Citadel.Core.Windows.Platform.Init();
            PlatformServices.RegisterPlatformServices(new WindowsPlatformServices());
        }
    }
}
