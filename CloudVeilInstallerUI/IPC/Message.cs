using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudVeilInstallerUI.IPC
{
    [Serializable]
    public enum Command
    {
        None,
        Call,
        Set,
        Get,
        Error,
        Response,
        GetResponse,
        Exit,
        Start,
        PropertyChanged
    }

    [Serializable]
    public class Message
    {
        public Message() : this(Guid.NewGuid())
        { 
        }

        public Message(Guid id)
        {
            Id = id;
        }

        public Guid Id { get; set; }

        public string VariableName { get; set; }
        public string Property { get; set; }
        public Command Command { get; set; }
        public object Data { get; set; }
    }
}
