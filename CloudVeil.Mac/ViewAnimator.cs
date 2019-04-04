// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
using System;
using AppKit;

namespace CloudVeil.Mac
{
    public class ViewAnimator : AppKit.NSViewControllerPresentationAnimator
    {
        public ViewAnimator()
        {
        }

        public override void AnimateDismissal(NSViewController viewController, NSViewController fromViewController)
        {
            var bottomVc = fromViewController;
            var topVc = viewController;

            topVc.View.WantsLayer = true;
            topVc.View.LayerContentsRedrawPolicy = NSViewLayerContentsRedrawPolicy.OnSetNeedsDisplay;

            NSAnimationContext.RunAnimation((context) =>
            {
                context.Duration = 1;

                (topVc.View.Animator as NSView).AlphaValue = 0;
            }, () =>
            {
                topVc.View.Superview.RemoveFromSuperview();
                topVc.View.RemoveFromSuperview();
            });
        }

        public override void AnimatePresentation(NSViewController viewController, NSViewController fromViewController)
        {
            var bottomVc = fromViewController;
            var topVc = viewController;

            topVc.View.WantsLayer = true;

            topVc.View.LayerContentsRedrawPolicy = NSViewLayerContentsRedrawPolicy.OnSetNeedsDisplay;

            topVc.View.AlphaValue = 1;

            NSVisualEffectView backgroundView = new NSVisualEffectView();
            backgroundView.Material = NSVisualEffectMaterial.Sheet;
            backgroundView.BlendingMode = NSVisualEffectBlendingMode.WithinWindow;
            backgroundView.AddSubview(topVc.View);
            backgroundView.AlphaValue = 0;

            bottomVc.View.AddSubview(backgroundView);

            backgroundView.LeadingAnchor.ConstraintEqualToAnchor(bottomVc.View.LeadingAnchor).Active = true;
            backgroundView.TopAnchor.ConstraintEqualToAnchor(bottomVc.View.TopAnchor).Active = true;
            backgroundView.BottomAnchor.ConstraintEqualToAnchor(bottomVc.View.BottomAnchor).Active = true;
            backgroundView.TrailingAnchor.ConstraintEqualToAnchor(bottomVc.View.TrailingAnchor).Active = true;

            topVc.View.LeadingAnchor.ConstraintEqualToAnchor(backgroundView.LeadingAnchor).Active = true;
            topVc.View.TopAnchor.ConstraintEqualToAnchor(backgroundView.TopAnchor).Active = true;
            topVc.View.BottomAnchor.ConstraintEqualToAnchor(backgroundView.BottomAnchor).Active = true;
            topVc.View.TrailingAnchor.ConstraintEqualToAnchor(backgroundView.TrailingAnchor).Active = true;

            backgroundView.Frame = bottomVc.View.Frame;
            topVc.View.Frame = bottomVc.View.Frame;
            
            NSAnimationContext.RunAnimation((context) =>
            {
                context.Duration = 1;
                (backgroundView.Animator as NSView).AlphaValue = 1;
            });
        }
    }
}
