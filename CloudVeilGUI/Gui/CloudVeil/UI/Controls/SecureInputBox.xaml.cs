/*
* Copyright © 2017 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System.Security;
using System.Windows;
using System.Windows.Controls;

namespace Gui.CloudVeil.UI.Controls
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