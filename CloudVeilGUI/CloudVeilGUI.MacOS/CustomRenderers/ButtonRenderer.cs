using System;
using System.ComponentModel;
using AppKit;
using Foundation;
using Xamarin.Forms;
using Xamarin.Forms.Platform.MacOS;

[assembly: ExportRenderer(typeof(CloudVeilGUI.CustomFormElements.Button), typeof(CloudVeilGUI.CustomRenderers.ButtonRenderer))]
namespace CloudVeilGUI.CustomRenderers
{
    public class ButtonRenderer : Xamarin.Forms.Platform.MacOS.ButtonRenderer
    {
        protected override void OnElementChanged(ElementChangedEventArgs<Button> e)
        {
            base.OnElementChanged(e);

            Control.BezelStyle = NSBezelStyle.Rounded;
            Control.Font = NSFont.LabelFontOfSize(12);
        }
    }
}