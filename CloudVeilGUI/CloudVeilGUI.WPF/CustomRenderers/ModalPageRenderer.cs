/*
* Copyright © 2017-2018 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CloudVeilGUI.CustomFormElements;
using CloudVeilGUI.WPF.CustomRenderers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WpfLightToolkit.Controls;
using Xamarin.Forms;
using Xamarin.Forms.Platform.WPF;
using XFPlatform = Xamarin.Forms.Platform.WPF.Platform;

[assembly: ExportRenderer(typeof(ModalPage), typeof(ModalPageRenderer))]
namespace CloudVeilGUI.WPF.CustomRenderers
{
    public class ModalPageRenderer : PageRenderer
    {
        protected override void OnElementChanged(ElementChangedEventArgs<Page> e)
        {
            base.OnElementChanged(e);

            Control.UpdateDependencyColor(LightContentPage.BackgroundProperty, new Color(0, 0, 0, 0.5));
            
            if(e.OldElement as ModalPage != null)
            {
                var hostPage = (ModalPage)e.OldElement;
                hostPage.CloseModalRequested -= OnCloseRequested;
            }

            if (e.NewElement as ModalPage != null)
            {
                var hostPage = (ModalPage)e.NewElement;
                hostPage.CloseModalRequested += OnCloseRequested;
            }
        }

        static async void OnCloseRequested(object sender, ModalPage.CloseModalRequestedEventArgs e)
        {
            var page = (ModalPage)sender;

            var element = XFPlatform.GetRenderer(page).GetNativeElement();

            (element.Parent as FormsLightNavigationPage)?.PopModal(true);
        }
    }
}
