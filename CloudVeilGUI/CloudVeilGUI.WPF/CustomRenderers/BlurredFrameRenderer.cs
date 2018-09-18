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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Xamarin.Forms;
using Xamarin.Forms.Platform.WPF;
using WRectangle = System.Windows.Shapes.Rectangle;
using WColor = System.Windows.Media.Color;
using XFColor = Xamarin.Forms.Color;
using System.ComponentModel;

[assembly: ExportRenderer(typeof(BlurredFrame), typeof(BlurredFrameRenderer))]
namespace CloudVeilGUI.WPF.CustomRenderers
{
    public class BlurredFrameRenderer : ViewRenderer<BlurredFrame, WRectangle>
    {
        Border _border;

        protected override void OnElementChanged(ElementChangedEventArgs<BlurredFrame> e)
        {
            if(e.NewElement != null)
            {
                if(Control == null)
                {
                    WRectangle rect = new WRectangle();

                    _border = new Border();

                    VisualBrush brush = new VisualBrush(_border);

                    rect.Fill = brush;

                    SetNativeControl(rect);
                }

                UpdateColor();
            }

            base.OnElementChanged(e);

        }

        protected override void OnElementPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            base.OnElementPropertyChanged(sender, e);

            if (e.PropertyName == BlurredFrame.BackgroundColorProperty.PropertyName)
            {
                UpdateColor();
            }
        }

        protected override void UpdateNativeWidget()
        {
            base.UpdateNativeWidget();

            UpdateSize();
        }

        void UpdateColor()
        {
            XFColor color = Element.BackgroundColor;
            _border.UpdateDependencyColor(Border.BackgroundProperty, new XFColor(color.R, color.G, color.B, 0.75));
        }

        void UpdateSize()
        {
            _border.Height = Element.Height > 0 ? Element.Height : Double.NaN;
            _border.Width = Element.Width > 0 ? Element.Width : Double.NaN;
        }
    }
}
