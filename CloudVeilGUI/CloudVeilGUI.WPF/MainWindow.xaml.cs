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

            var app = new CloudVeilGUI.App();
            LoadApplication(app);

            //((App)App.Current).SessionEnding += MainWindow_SessionEnding;
        }
    }
}
