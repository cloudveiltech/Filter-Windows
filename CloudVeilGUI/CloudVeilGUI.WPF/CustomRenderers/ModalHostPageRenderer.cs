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
using Xamarin.Forms;
using Xamarin.Forms.Platform.WPF;
using XFPlatform = Xamarin.Forms.Platform.WPF.Platform;

[assembly: ExportRenderer(typeof(ModalHostPage), typeof(ModalHostPageRenderer))]
namespace CloudVeilGUI.WPF.CustomRenderers
{
    public class ModalHostPageRenderer : NavigationPageRenderer
    {
        protected override void OnElementChanged(ElementChangedEventArgs<NavigationPage> e)
        {
            base.OnElementChanged(e);

            if(e.OldElement as ModalHostPage != null)
            {
                var hostPage = (ModalHostPage)e.OldElement;
                hostPage.DisplayPageModalRequested -= OnDisplayPageModalRequested;
            }

            if (e.NewElement as ModalHostPage != null)
            {
                var hostPage = (ModalHostPage)e.NewElement;
                hostPage.DisplayPageModalRequested += OnDisplayPageModalRequested;
            }
        }

        void OnDisplayPageModalRequested(object sender, ModalHostPage.DisplayPageModalRequestedEventArgs e)
        {
            e.PageToDisplay.Parent = this.Element;
            IVisualElementRenderer renderer = XFPlatform.GetRenderer(e.PageToDisplay);

            if(renderer == null)
            {
                renderer = XFPlatform.CreateRenderer(e.PageToDisplay);
                XFPlatform.SetRenderer(e.PageToDisplay, renderer);
            }

            // TODO: Now display our modal page.
            var modalElement = renderer.GetNativeElement();

            (Control as FormsLightNavigationPage)?.PushModal(modalElement, true);
        }
    }
}
