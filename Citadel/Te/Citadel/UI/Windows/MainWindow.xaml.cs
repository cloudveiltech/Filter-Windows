using System;
using Te.Citadel.Util;

namespace Te.Citadel.UI.Windows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : BaseWindow
    {
        public MainWindow()
        {
            InitializeComponent();

            try
            {
                // Show binary version # in the title bar.
                string title = System.Diagnostics.Process.GetCurrentProcess().ProcessName;

                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                title += " - Version " + System.Reflection.AssemblyName.GetAssemblyName(assembly.Location).Version.ToString();
                this.Title = title;
            }
            catch(Exception e)
            {
                LoggerUtil.RecursivelyLogException(NLog.LogManager.GetLogger("Citadel"), e);
            }
        }
    }
}