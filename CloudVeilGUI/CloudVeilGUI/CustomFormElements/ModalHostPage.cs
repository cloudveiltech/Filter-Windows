// Copied from https://github.com/gaborv/xam-forms-transparent-modal/blob/master/CustomFormElements/ModalHostPage.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace CloudVeilGUI.CustomFormElements
{
	public class ModalHostPage : NavigationPage, IModalHost
	{
		public ModalHostPage (Page page) : base(page)
		{

		}

        public Task DisplayPageModal(Page page)
        {
            var displayEvent = DisplayPageModalRequested;

            Task completion = null;
            if (displayEvent != null)
            {
                var eventArgs = new DisplayPageModalRequestedEventArgs(page);
                displayEvent(this, eventArgs);
                completion = eventArgs.DisplayingPageTask;
            }

            // If there is not task, just create a new completed one
            return completion ?? Task.FromResult<object>(null);
        }

        public event EventHandler<DisplayPageModalRequestedEventArgs> DisplayPageModalRequested;

        public sealed class DisplayPageModalRequestedEventArgs : EventArgs
        {
            public Task DisplayingPageTask { get; set; }

            public Page PageToDisplay { get; }

            public DisplayPageModalRequestedEventArgs(Page modalPage)
            {
                PageToDisplay = modalPage;
            }
        }
    }
}