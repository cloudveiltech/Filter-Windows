using AppKit;
using Xamarin.Forms;
using CloudVeilGUI.MacOS;
using Xamarin.Forms.Platform.MacOS;

[assembly: ResolutionGroupName("CloudVeil")]
[assembly: ExportEffect(typeof(MacOSButtonEffect), "ButtonEffect")]
namespace CloudVeilGUI.MacOS
{
    public class MacOSButtonEffect : PlatformEffect
    {
        /*public ButtonEffect()
            : base("CloudVeil.ButtonEffect")
        {
        }*/

        private void AddButtonStyle()
        {
            NSButton button = (NSButton)Control;

            if(button == null) {
                return;
            }

            button.BezelStyle = NSBezelStyle.Rounded;
        }

        private void RemoveButtonStyle() {
            NSButton button = (NSButton)Control;

            if(button == null) {
                return;
            }

            button.BezelStyle = 0;
        }

        protected override void OnAttached()
        {
            AddButtonStyle();
        }

        protected override void OnDetached()
        {
            RemoveButtonStyle();
        }
    }
}
