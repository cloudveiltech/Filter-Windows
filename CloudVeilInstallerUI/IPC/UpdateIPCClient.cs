using NamedPipeWrapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudVeilInstallerUI.IPC
{
    public class UpdateIPCClient : IPCCommunicator
    {
        NamedPipeClient<Message> client;

        public UpdateIPCClient(string pipeName)
        {
            client = new NamedPipeClient<Message>(pipeName);
            client.AutoReconnect = true;

            client.ServerMessage += OnMessageReceived;
        }

        public void Start() => client.Start();
        public void WaitForConnection() => client.WaitForConnection();

        public override void PushMessage(Message message)
        {
            try
            {
                client.PushMessage(message);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}
