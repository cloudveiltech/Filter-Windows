using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WpfLightToolkit.Controls;

namespace CloudVeil.Windows.Views
{
    public partial class Modal : LightContentPage
    {

        private LightWindow parentWindow;

        public Modal(LightWindow parentWindow, string title, string message, string okButton)
        {
            InitializeComponent();
            this.parentWindow = parentWindow;

            this.okButton.Content = okButton;
            this.titleLabel.Content = title;
            this.messageLabel.Content = message;

            this.okButton.Click += OkButton_Click;
        }

        private void OkButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            this.parentWindow.PopModal(true);
        }

    }
}