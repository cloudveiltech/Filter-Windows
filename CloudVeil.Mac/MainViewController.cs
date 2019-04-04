// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
using System;
using System.Collections.Generic;
using AppKit;
using CloudVeil.Mac.Platform;
using CloudVeil.Mac.Views;
using Foundation;

namespace CloudVeil.Mac
{
    public partial class MainViewController : NSSplitViewController
    {
        public MainViewController(IntPtr handle) : base(handle)
        {
            MacGuiServices.MainViewController = this;
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            // Do any additional setup after loading the view.
        }

        public override NSObject RepresentedObject
        {
            get
            {
                return base.RepresentedObject;
            }
            set
            {
                base.RepresentedObject = value;
                // Update the view, if already loaded.
            }
        }

        private Stack<NSViewController> modalViewControllers = new Stack<NSViewController>();

        public void ShowLoginView()
        {
            LoginViewController viewController = Storyboard.InstantiateControllerWithIdentifier("LoginViewController") as LoginViewController;
            PresentViewController(viewController, new ViewAnimator());

            modalViewControllers.Push(viewController);
        }

        public void ShowWaitingView()
        {
            NSViewController viewController = Storyboard.InstantiateControllerWithIdentifier("WaitingViewController") as NSViewController;
            PresentViewController(viewController, new ViewAnimator());

            modalViewControllers.Push(viewController);
        }

        public void DismissAllModals()
        {
            while (modalViewControllers.Count > 0)
            {
                DismissViewController(modalViewControllers.Pop());
            }
        }
    }
}
