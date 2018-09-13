/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using CloudVeilGUI.ViewModels;
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