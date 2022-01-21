/*
* Copyright © 2019 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gui.CloudVeil.UI.ViewModels;
using Gui.CloudVeil.UI.Views;
using Gui.CloudVeil.UI.Windows;

namespace Gui.CloudVeil.UI
{
    public class ViewManager : ModelManager
    {
        private class ViewWithZIndex
        {
            public int ZIndex { get; set; }
            public Type ViewType { get; set; }
        }

        public ViewManager(MainWindow window)
        {
            this.window = window;
            this.views = new List<ViewWithZIndex>();
        }

        private MainWindow window;
        private List<ViewWithZIndex> views;

        public void Register<T>(T view, Action<BaseView> viewModelAction)
        {
            if (view is BaseView)
            {
                var context = (view as BaseView).DataContext;

                if(context is BaseCloudVeilViewModel)
                {
                    viewModelAction?.Invoke(view as BaseView);
                }
            }

            Register(view);
        }

        /// <summary>
        /// Sets the base view.
        /// </summary>
        /// <remarks>
        /// Should not be called more than once per runtime session, but handling is built in if that does happen.
        /// </remarks>
        /// <param name="t">The type of the view to show.</param>
        public void SetBaseView(Type t)
        {
            for(int i = 0; i < views.Count; i++)
            {
                if(views[i].ZIndex == 0)
                {
                    views.RemoveAt(i);
                    i--;
                    continue;
                }
            }

            views.Add(new ViewWithZIndex()
            {
                ZIndex = 0,
                ViewType = t
            });

            showViewWithHighestZIndex();
        }

        private ViewWithZIndex getView(Type t, int zIndex = -1)
        {
            for (int i = 0; i < views.Count; i++)
            {
                if(views[i].ViewType == t && (zIndex == -1 || views[i].ZIndex == zIndex))
                {
                    return views[i];
                }
            }

            return null;
        }

        private void showViewWithHighestZIndex()
        {
            int highestZIndex = 0;
            ViewWithZIndex viewToShow = null;

            foreach(var view in views)
            {
                if(view.ZIndex > highestZIndex || viewToShow == null)
                {
                    viewToShow = view;
                    highestZIndex = view.ZIndex;
                }
            }

            if(viewToShow != null)
            {
                var view = this.Get(viewToShow.ViewType);

                window.Dispatcher.InvokeAsync(() =>
                {
                    if (view != null && window.CurrentView.Content != view)
                    {
                        window.CurrentView.Content = view;
                    }
                });
            }
        }

        public void PushView(int zIndex, Type t)
        {
            var existingView = getView(t);
            if(existingView == null)
            {
                views.Add(new ViewWithZIndex()
                {
                    ZIndex = zIndex,
                    ViewType = t
                });
            }

            showViewWithHighestZIndex();
        }

        public void PopView(Type t, int zIndex = -1)
        {
            var existingView = getView(t, zIndex);
            if(existingView != null)
            {
                views.Remove(existingView);
            }

            showViewWithHighestZIndex();
        }
    }
}
