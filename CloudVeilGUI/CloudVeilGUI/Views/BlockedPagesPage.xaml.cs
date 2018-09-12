/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using CloudVeilGUI.Models;
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
            BindingContext = viewModel;
		}
    }
}