using CloudVeilInstallerUI;
using CloudVeilInstallerUI.IPC;
using CloudVeilInstallerUI.ViewModels;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace CloudVeilUpdater
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            UpdateIPCClient client = new UpdateIPCClient("__CloudVeilUpdaterPipe__");

            RemoteInstallerViewModel model = new RemoteInstallerViewModel(client);
            ISetupUI setupUi = null;

            setupUi = new MainWindow(model, true);

            client.RegisterObject("SetupUI", setupUi);

            client.Start();

            Console.WriteLine("Client Waiting for connection");
            client.WaitForConnection();
            Console.WriteLine("Client connected");

            client.PushMessage(new Message()
            {
                Command = Command.Start
            });

            setupUi.Closed += (sender, _e) =>
            {
                client.PushMessage(new Message()
                {
                    Command = Command.Exit
                });
            };

            setupUi.Show();

            base.OnStartup(e);
        }
    }
}
