using CloudVeilGUI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace CloudVeilGUI.Views
{
	[XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class SelfModerationPage : ContentPage
	{
        SelfModerationViewModel viewModel;

		public SelfModerationPage ()
		{
			InitializeComponent ();

            BindingContext = viewModel = new SelfModerationViewModel();

		}
	}
}