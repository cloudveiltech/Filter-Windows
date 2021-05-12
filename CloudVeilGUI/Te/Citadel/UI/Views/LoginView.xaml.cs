/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System.Windows.Forms;
namespace Te.Citadel.UI.Views
{
    /// <summary>
    /// Interaction logic for LoginView.xaml
    /// </summary>
    public partial class LoginView : BaseView
    {
        public static int ModalZIndex = 200;

        public LoginView()
        {
            InitializeComponent();
        }

        private Keys m_keyCode;

        private void Form1_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            this.m_keyCode = e.KeyCode;
        }

        private void True(object sender, System.Windows.Input.KeyEventArgs e)
        {

        }
    }
}