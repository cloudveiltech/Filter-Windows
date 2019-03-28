// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
using AppKit;
using CloudVeil.Mac.Platform;
using CloudVeilGUI.Common;
using CloudVeilGUI.Platform.Common;
using Filter.Platform.Common;

namespace CloudVeil.Mac
{
    static class MainClass
    {
        static void Main(string[] args)
        {
            PlatformTypes.Register<IGuiServices>((arr) => new MacGuiServices());
            PlatformTypes.Register<IFilterStarter>((arr) => new MacFilterStarter());
            PlatformTypes.Register<ITrayIconController>((arr) => new MacTrayIconController());

            Filter.Platform.Mac.Platform.Init();

            CommonAppServices.Default.Init();

            NSApplication.Init();
            NSApplication.Main(args);
        }
    }
}
