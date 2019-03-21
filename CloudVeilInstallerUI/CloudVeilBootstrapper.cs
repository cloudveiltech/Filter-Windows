using CloudVeilInstallerUI.IPC;
using CloudVeilInstallerUI.ViewModels;
using Microsoft.Tools.WindowsInstallerXml.Bootstrapper;
using NamedPipeWrapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace CloudVeilInstallerUI
{
    public interface IBootstrapper
    {

    }

    public class LocalBootstrapperImpl : IBootstrapper
    {
        CloudVeilBootstrapper ba;

        public LocalBootstrapperImpl(CloudVeilBootstrapper ba)
        {
            this.ba = ba;
        }
    }

    public class IpcBootstrapperImpl : IBootstrapper
    {
        CloudVeilBootstrapper ba;

        public IpcBootstrapperImpl(CloudVeilBootstrapper ba)
        {
            this.ba = ba;
        }
    }

    public class CloudVeilBootstrapper : BootstrapperApplication
    {
        public static Dispatcher BootstrapperDispatcher { get; private set; }

        protected override void Run()
        {
            try
            {
                string[] args = this.Command.GetCommandLineArgs();

                bool runIpc = false;
                bool showPrompts = true;

                Engine.Log(LogLevel.Standard, $"Arguments: {string.Join(", ", args)}");
                foreach (string arg in args)
                {
                    if (arg == "/ipc")
                    {
                        runIpc = true;
                    }
                    else if (arg == "/nomodals")
                    {
                        showPrompts = false;
                    }
                }

                BootstrapperDispatcher = Dispatcher.CurrentDispatcher;

                Application app = new Application();

                ISetupUI setupUi = null;
                InstallerViewModel model = new InstallerViewModel(this);
                UpdateIPCServer server = null;

                if (runIpc)
                {
                    server = new UpdateIPCServer("__CloudVeilUpdaterPipe__");
                    server.MessageReceived += CheckExit;

                    server.RegisterObject("InstallerViewModel", model);
                    server.RegisterObject("SetupUI", setupUi);
                    server.Start();

                    server.MessageReceived += CheckStartCommand; // Wait for the first start command to begin installing.

                    setupUi = new IpcWindow(server, model, showPrompts);
                    model.SetSetupUi(setupUi);

                    model.PropertyChanged += (sender, e) =>
                    {
                        server.PushMessage(new Message()
                        {
                            Command = IPC.Command.PropertyChanged,
                            Property = e.PropertyName
                        });
                    };

                    this.Engine.Detect();
                }
                else
                {
                    setupUi = new MainWindow(model, showPrompts);
                    setupUi.Closed += (sender, e) => BootstrapperDispatcher.InvokeShutdown();

                    model.SetSetupUi(setupUi);
                    this.Engine.Detect();

                    setupUi.Show();
                    Dispatcher.Run();
                    this.Engine.Quit(0);
                }
            }
            catch(Exception ex)
            {
                Engine.Log(LogLevel.Error, "A .NET error occurred while running CloudVeilInstallerUI");
                Engine.Log(LogLevel.Error, $"Error Type: {ex.GetType().Name}");
                Engine.Log(LogLevel.Error, $"Error info: {ex}");

                this.Engine.Quit(1);
            }
        }

        private void CheckStartCommand(NamedPipeConnection<Message, Message> connection, Message message)
        {
            if(message.Command == IPC.Command.Start)
            {
                Engine.Log(LogLevel.Standard, "Start command received. Starting new thread for dispatcher.");

                Thread t = new Thread(() =>
                {
                    Dispatcher.Run();
                    Engine.Quit(0);
                });

                t.Start();
            }
        }

        private void CheckExit(NamedPipeConnection<Message, Message> connection, Message message)
        {
            if(message.Command == IPC.Command.Exit)
            {
                BootstrapperDispatcher.InvokeShutdown();
            }
        }
    }
}
