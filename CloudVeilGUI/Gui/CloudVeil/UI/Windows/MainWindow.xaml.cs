/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CloudVeil.Core.Windows.Client;
using CloudVeil.Core.Windows.Util;
using CloudVeil.Core.Windows.WinAPI;
using CloudVeil.Windows;
using Filter.Platform.Common.Util;
using Gui.CloudVeil.UI.ViewModels;
using Sentry.Protocol;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Gui.CloudVeil.UI.Windows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml 
    /// </summary>
    public partial class MainWindow : BaseWindow
    {
        public MainWindow()
        {
            InitializeComponent();

            this.Activated += MainWindow_Activated;
        }

        private void MainWindow_Activated(object sender, EventArgs e)
        {
            try
            {
                // Show binary version # in the title bar.
                string title = System.Diagnostics.Process.GetCurrentProcess().ProcessName;

                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                title += " - Version " + System.Reflection.AssemblyName.GetAssemblyName(assembly.Location).Version.ToString(3);
                this.Title = title;
            }
            catch(Exception err)
            {
                LoggerUtil.RecursivelyLogException(LoggerUtil.GetAppWideLogger(), err);
            }
        }


        public MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(e.Uri.ToString());
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
            source.AddHook(WndProc);
        }

        IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            var app = (Application.Current as CloudVeilApp);
            if (msg == (int)WindowMessages.CV_SHOW_WINDOW)
            {
                app.BringAppToFocus();
            }
            if (msg == (int)WindowMessages.WM_COPY_DATA)
            {

                try
                {
                    if (lParam != IntPtr.Zero)
                    {
                        var cds = Marshal.PtrToStructure<COPYDATASTRUCT>(lParam);

                        if (cds.lpData != IntPtr.Zero)
                        {
                            app.processCustomScheme(cds.AsUnicodeString);
                        }
                    }
                }
                catch
                {
                }

                app.BringAppToFocus();
            }

            return IntPtr.Zero;
        }

    }
}