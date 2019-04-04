using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CloudVeilInstallerUI.IPC;
using CloudVeilInstallerUI.Models;
using NamedPipeWrapper;

namespace CloudVeilInstallerUI.ViewModels
{
    public class RemoteInstallerViewModel : IInstallerViewModel
    {

        UpdateIPCClient client;

        public RemoteInstallerViewModel(UpdateIPCClient client)
        {
            this.client = client;

            client.MessageReceived += CheckPropertyChangedMessage;
        }

        private void CheckPropertyChangedMessage(NamedPipeConnection<Message, Message> connection, Message message)
        {
            if(message.Command == Command.PropertyChanged)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(message.Property));
            }
        }

        private TRet get<TRet>(string prop)
        {
            Task<object> o = client.Get("InstallerViewModel", prop);
            o.Wait();

            return (TRet)o.Result;
        }

        private void set<TSettable>(string prop, TSettable val)
        {
            client.Set("InstallerViewModel", prop, val);
        }

        private Task<TRet> call<TRet>(string method, params object[] parameters)
        {
            Task<object> o = client.Call("InstallerViewModel", method, parameters);
            return o.ContinueWith<TRet>((t) =>
            {
                return (TRet)t.Result;
            });
        }

        public string WelcomeButtonText
        {
            get => get<string>(nameof(WelcomeButtonText));
            set => set(nameof(WelcomeButtonText), value);
        }

        public string WelcomeHeader { get => get<string>(nameof(WelcomeHeader)); set => set(nameof(WelcomeHeader), value); }
        public string WelcomeText { get => get<string>(nameof(WelcomeText)); set => set(nameof(WelcomeText), value); }
        public bool ShowPrompts { get => get<bool>(nameof(ShowPrompts)); set => set(nameof(ShowPrompts), value); }
        public InstallType InstallType { get => get<InstallType>(nameof(InstallType)); set => set(nameof(InstallType), value); }
        public InstallationState State { get => get<InstallationState>(nameof(State)); set => set(nameof(State), value); }
        public string Description { get => get<string>(nameof(Description)); set => set(nameof(Description), value); }
        public bool IsIndeterminate { get => get<bool>(nameof(IsIndeterminate)); set => set(nameof(IsIndeterminate), value); }
        public int Progress { get => get<int>(nameof(Progress)); set => set(nameof(Progress), value); }
        public string FinishedHeading { get => get<string>(nameof(FinishedHeading)); set => set(nameof(FinishedHeading), value); }
        public string FinishedMessage { get => get<string>(nameof(FinishedMessage)); set => set(nameof(FinishedMessage), value); }
        public string FinishButtonText { get => get<string>(nameof(FinishButtonText)); set => set(nameof(FinishButtonText), value); }
        public bool NeedsRestart { get => get<bool>(nameof(NeedsRestart)); set => set(nameof(NeedsRestart), value); }

        public event PropertyChangedEventHandler PropertyChanged;

        public void TriggerFailed(string message, string heading = null, bool needsRestart = false) => call<object>("TriggerFailed", message, heading, needsRestart);
        public void TriggerFinished() => call<object>("TriggerFinished");
        public void TriggerInstall() => call<object>("TriggerInstall");
        public void TriggerLicense() => call<object>("TriggerLicense");
        public void TriggerWelcome() => call<object>("TriggerWelcome");
        public void StartFilterIfExists() => call<object>("StartFilterIfExists");

        public void Exit()
        {
            call<object>("Exit");
            Application.Current.Shutdown(0);
        }
    }
}
