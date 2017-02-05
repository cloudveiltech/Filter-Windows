/*
* Copyright © 2017 Jesse Nicholson  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

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
                LoggerUtil.RecursivelyLogException(LoggerUtil.GetAppWideLogger(), e);
            }
        }
    }
}