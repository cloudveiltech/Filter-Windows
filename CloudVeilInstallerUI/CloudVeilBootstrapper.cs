using CloudVeil.Core.Windows.Util;
using CloudVeilInstallerUI.IPC;
using CloudVeilInstallerUI.Models;
using CloudVeilInstallerUI.ViewModels;
using Microsoft.Tools.WindowsInstallerXml.Bootstrapper;
using NamedPipeWrapper;
using Sentry;
using System;
using System.Diagnostics;
using System.Threading;
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

        public bool WaitForFilterExit { get; set; } = false;

        public bool Updating { get; private set; } = false;

        public string UserId { get; private set; } = "";
        private EventWaitHandle exitWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);

        private IDisposable sentry;
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
                sentry = SentrySdk.Init(CloudVeil.CompileSecrets.SentryDsn);
            } catch
            {
                sentry = null;
            }
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
                    else if (arg == "/waitforexit")
                    {
                        WaitForFilterExit = true;
                    }
                    else if (arg == "/upgrade")
                    {
                        Updating = true;
                    }
                    else if (arg.Contains("/userid="))
                    {
                        UserId = arg.Replace("/userid=", "");
                    }
                }

                if (UserId.Length > 0)
                {
                    _ = WebUtil.PostVersionStringAsync(UserId);
                }

                if(Updating == false && WaitForFilterExit == true)
                {
                    Updating = true;
                }
                if (UserId.Length == 0)
                {
                    try
                    {
                        var email = new RegistryAuthenticationStorage().UserEmail;
                        var fingerPrint = new WindowsFingerprint().Value;
                        UserId = email + ":" + fingerPrint;
                        Engine.Log(LogLevel.Standard, $"My autoset id: {UserId}");
                    } 
                    catch
                    {
                        Engine.Log(LogLevel.Error, "Can't set User id, probably fresh install");
                    }
                }

                if (Updating)
                {
                    var checker = new InstallerCheckPackageCache.InstallerCacheChecker();
                    checker.CheckAndRestoreCache();
                    tryCloseGuiClient();
                }

                BootstrapperDispatcher = Dispatcher.CurrentDispatcher;

                Application app = new Application();

                ISetupUI setupUi = null;
                InstallerViewModel model = new InstallerViewModel(this);

                // NOTE: This runIpc check can be removed if our current system proves itself.
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

                    if (Command.Display != Display.None && Command.Display != Display.Embedded)
                    {
                        setupUi.Show();
                    }

                    Dispatcher.Run();

                    if (sentry != null)
                    {
                        sentry.Dispose();
                    }
                    this.Engine.Quit(0);
                }
            }
            catch (Exception ex)
            {
                Engine.Log(LogLevel.Error, "A .NET error occurred while running CloudVeilInstallerUI");
                Engine.Log(LogLevel.Error, $"Error Type: {ex.GetType().Name}");
                Engine.Log(LogLevel.Error, $"Error info: {ex}");

                this.Engine.Quit(1);
            }
        }

        private void CheckStartCommand(NamedPipeConnection<Message, Message> connection, Message message)
        {
            if (message.Command == IPC.Command.Start)
            {
                Engine.Log(LogLevel.Standard, "Start command received. Starting new thread for dispatcher.");

                Thread t = new Thread(() =>
                {
                    while (!IsExiting)
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
            if (message.Command == IPC.Command.Exit)
            {
                sentry.Dispose();
                SignalExit();
            }
        }

        private void tryCloseGuiClient()
        {
            Engine.Log(LogLevel.Error, "tryCloseGuiClient");

            foreach (Process process in Process.GetProcessesByName("CloudVeil"))
            {
                Engine.Log(LogLevel.Error, "found, try to kill");
                process.Kill();
            }
        }
    }
}
