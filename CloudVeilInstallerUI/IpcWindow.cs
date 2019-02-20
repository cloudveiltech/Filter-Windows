using CloudVeilInstallerUI.IPC;
using CloudVeilInstallerUI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public event EventHandler Closed;

        public void Close()
        {
            Closed?.Invoke(this, new EventArgs());
        }

        public void Show()
        {
            server.Call("SetupUI", "Show", new object[] { }).Wait();
        }

        public void ShowFinish()
        {
            server.Call("SetupUI", "ShowFinish", new object[] { }).Wait();
        }

        public void ShowInstall()
        {
            server.Call("SetupUI", "ShowInstall", new object[] { }).Wait();
        }

        public void ShowWelcome()
        {
            server.Call("SetupUI", "ShowWelcome", new object[] { }).Wait();
        }
    }
}
