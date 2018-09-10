using Citadel.IPC;
using System;
using System.Collections.Generic;
using System.Text;

namespace CloudVeilGUI.Platform.Common
{
    public abstract class PlatformServices
    {
        private static PlatformServices s_default;
        public static PlatformServices Default
        {
            get
            {
                if(s_default == null)
                {
                    throw new Exception("Platform GUI did not implement a PlatformServices singleton. Please register one with RegisterPlatformServices(...)");
                }

                return s_default;
            }
        }

        public static void RegisterPlatformServices(PlatformServices services)
        {
            s_default = services;
        }

        public abstract IFilterStarter CreateFilterStarter();
    }
}
