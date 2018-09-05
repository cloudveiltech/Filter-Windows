using Xamarin.Forms.Platform.WPF;
using Xamarin.Forms;

namespace CloudVeilGUI.WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : FormsApplicationPage
    {
        public MainWindow()
        {
            InitializeComponent();

            Forms.Init();

            LoadApplication(new CloudVeilGUI.App());
        }
    }
}
