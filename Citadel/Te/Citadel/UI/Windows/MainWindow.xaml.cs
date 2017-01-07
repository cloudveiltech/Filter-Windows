using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
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

                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                title += " - Version " + System.Reflection.AssemblyName.GetAssemblyName(assembly.Location).Version.ToString();
                this.Title = title;
            }
            catch (Exception e)
            {   
                LoggerUtil.RecursivelyLogException(NLog.LogManager.GetLogger("Citadel"), e);
            }
        }
    }
}
