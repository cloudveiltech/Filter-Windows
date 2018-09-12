using System;
using System.Collections.Generic;
using System.Text;

namespace CloudVeilGUI.IPCHandlers
{
    public class CallbackBase
    {
        protected App app;

        public CallbackBase(App app)
        {
            this.app = app;
        }
    }
}
