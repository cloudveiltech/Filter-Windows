using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Te.Citadel.Extensions
{
    public static class UIExtensions
    {
        public static void Disconnect(this UIElement child)
        {   
            var parent = VisualTreeHelper.GetParent(child);

            if(parent == null)
            {
                Debug.WriteLine("can't disconnect from nothing.");
                return;
            }

            var panel = parent as Panel;

            if (panel != null)
            {
                panel.Children.Remove(child);
                Debug.WriteLine("panel");
                return;
            }

            var decorator = parent as Decorator;
            if (decorator != null)
            {
                if (decorator.Child == child)
                {
                    decorator.Child = null;
                }
                Debug.WriteLine("dec");
                return;
            }

            var contentPresenter = parent as ContentPresenter;
            if (contentPresenter != null)
            {
                if (contentPresenter.Content == child)
                {
                    contentPresenter.Content = null;
                }
                Debug.WriteLine("cp");
                return;
            }

            var contentControl = parent as ContentControl;
            if (contentControl != null)
            {
                if (contentControl.Content == child)
                {
                    contentControl.Content = null;
                }
                Debug.WriteLine("cc");
                return;
            }

            var itemsControl = parent as ItemsControl;
            if (itemsControl != null)
            {
                itemsControl.Items.Remove(child);
                Debug.WriteLine("ic");
                return;
            }

            Debug.WriteLine(parent.GetType().Name);

            Debug.WriteLine("Not disconnected");
        }
    }
}