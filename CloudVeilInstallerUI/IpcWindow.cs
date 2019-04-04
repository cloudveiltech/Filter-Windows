using CloudVeilInstallerUI.IPC;
using CloudVeilInstallerUI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace CloudVeilInstallerUI
{
    public class IpcWindow : ISetupUI
    {
        private IInstallerViewModel viewModel;

        public IpcWindow(UpdateIPCServer server, IInstallerViewModel viewModel, bool showPrompts)
        {
            this.server = server;
            this.viewModel = viewModel;
        }

        public UpdateIPCServer server;
        public IntPtr Hwnd => IntPtr.Zero;
        public Dispatcher Dispatcher => null;

        public event EventHandler Closed;

        public void Close()
        {
            Closed?.Invoke(this, new EventArgs());
        }

        private object lastCalledLock = new object();

        private string lastCalled = null;
        private object[] lastCalledArgs = null;

        public void ResynchronizeUI()
        {
            if(lastCalled != null && lastCalledArgs != null)
            {
                server.Call("SetupUI", lastCalled, lastCalledArgs).Wait();
            }
        }

        private Task<object> storeAndCall(string fn, object[] args)
        {
            lock(lastCalledLock)
            {
                lastCalled = fn;
                lastCalledArgs = args;
            }

            return server.Call("SetupUI", fn, args);
        }

        public void Show() => storeAndCall("Show", new object[] { });
        public void ShowFinish() => storeAndCall("ShowFinish", new object[] { });
        public void ShowInstall() => storeAndCall("ShowInstall", new object[] { });
        public void ShowLicense() => storeAndCall("ShowLicense", new object[] { });
        public void ShowWelcome() => storeAndCall("ShowWelcome", new object[] { });
    }
}
