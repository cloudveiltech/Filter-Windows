using CloudVeilGUI.Models;
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
	public partial class BlockedPagesPage : ContentPage
	{
        private BlockedPagesViewModel viewModel;

		public BlockedPagesPage ()
		{
			InitializeComponent ();

            var app = (App)Application.Current;

            var model = app.ModelManager.GetModel<BlockedPagesModel>();

            viewModel = new BlockedPagesViewModel(model);
		}
	}
}