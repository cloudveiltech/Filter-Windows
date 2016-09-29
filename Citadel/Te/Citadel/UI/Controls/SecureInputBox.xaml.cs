using System.Diagnostics;
using System.Security;
using System.Windows;
using System.Windows.Controls;

namespace Te.Citadel.UI.Controls
{
    /// <summary>
    /// Interaction logic for SecureInputBox.xaml
    /// </summary>
    public partial class SecureInputBox : UserControl
    {
        public static readonly DependencyProperty PasswordProperty = DependencyProperty.Register(
            "Password",
            typeof(SecureString),
            typeof(SecureInputBox),
            new PropertyMetadata(default(SecureString))
            );

        public SecureString Password
        {
            get
            {
                return (SecureString)GetValue(PasswordProperty);
            }

            set
            {
                SetValue(PasswordProperty, value);
            }
        }

        public SecureInputBox()
        {
            InitializeComponent();

            // Update DependencyProperty whenever the password changes
            m_passwordBox.PasswordChanged += (sender, args) =>
            {                
                Password = ((PasswordBox)sender).SecurePassword;
            };
        }
    }
}