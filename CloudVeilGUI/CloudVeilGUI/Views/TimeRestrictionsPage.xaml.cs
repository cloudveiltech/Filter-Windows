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
	public partial class TimeRestrictionsPage : ContentPage
	{
        TimeRestrictionsViewModel viewModel;

		public TimeRestrictionsPage ()
		{
			InitializeComponent ();

            BindingContext = viewModel = new TimeRestrictionsViewModel();
            viewModel.IsTimeRestrictionsEnabled = true;
            viewModel.FromTime = new TimeSpan(19, 0, 0);
            viewModel.ToTime = new TimeSpan(5, 0, 0);
		}
	}
}