using CloudVeilInstallerUI.IPC;
using CloudVeilInstallerUI.ViewModels;
using Microsoft.Tools.WindowsInstallerXml.Bootstrapper;
using NamedPipeWrapper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        public bool IsExiting { get; set; } = false;

        private EventWaitHandle exitWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);

        public void SignalExit()
        {
            IsExiting = true;
            exitWaitHandle.Set();
        }

        UpdateIPCServer server = null;

        protected override void Run()
        {
            try
            {
                string[] args = this.Command.GetCommandLineArgs();

                bool runIpc = false;
                bool showPrompts = true;
                bool waitForFilterExit = false;

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
                    else if(arg == "/waitforexit")
                    {
                        waitForFilterExit = true;
                    }
                }

                BootstrapperDispatcher = Dispatcher.CurrentDispatcher;

                Application app = new Application();

                ISetupUI setupUi = null;
                InstallerViewModel model = new InstallerViewModel(this);

                if (waitForFilterExit)
                {
                    while (true)
                    {
                        Process[] fsp = Process.GetProcessesByName("FilterServiceProvider");
                        if (fsp.Length > 0)
                        {
                            // Check for HasExited, since the process may still be in the list, even though it has exited.
                            try
                            {
                                bool allHaveExited = true;
                                foreach (Process p in fsp)
                                {
                                    if (!p.HasExited)
                                    {
                                        allHaveExited = false;
                                        break; // Break out from inner foreach loop.
                                    }
                                }

                                if(allHaveExited)
                                {
                                    break;
                                }
                            }
                            catch (Exception)
                            {
                                break;
                            }


                            Thread.Sleep(100);
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                if (runIpc)
                {
                    if (server == null)
                    {
                        server = new UpdateIPCServer(UpdateIPCServer.PipeName);

                        server.MessageReceived += CheckExit;

                        server.RegisterObject("InstallerViewModel", model);

                        server.Start();
                    }

                    setupUi = new IpcWindow(server, model, showPrompts);
                    server.RegisterObject("SetupUI", setupUi);

                    server.MessageReceived += CheckStartCommand; // Wait for the first start command to begin installing.
                    
                    setupUi.Closed += (sender, e) => SignalExit();

                    server.ClientConnected += () =>
                    {
                        Engine.Log(LogLevel.Standard, "Resynchronizing UI with new client.");
                        (setupUi as IpcWindow)?.ResynchronizeUI();
                    };

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
                    setupUi.Closed += (sender, e) =>
                    {
                        Engine.Log(LogLevel.Standard, "Closing installer.");
                        BootstrapperDispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
                        Engine.Log(LogLevel.Standard, "Shutdown invoked.");
                    };

                    model.SetSetupUi(setupUi);

                    Engine.Detect();
                    
                    if(Command.Display != Display.None && Command.Display != Display.Embedded)
                    {
                        setupUi.Show();
                    }

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
                    while(!IsExiting)
                    {
                        exitWaitHandle.WaitOne(2000);
                    }

                    Engine.Quit(0);
                });

                t.Start();
            }
        }

        private void CheckExit(NamedPipeConnection<Message, Message> connection, Message message)
        {
            if(message.Command == IPC.Command.Exit)
            {
                SignalExit();
            }
        }
    }
}
