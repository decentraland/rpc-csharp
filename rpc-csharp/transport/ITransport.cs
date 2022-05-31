using System;

namespace rpc_csharp.transport
{
    public interface ITransport
    {
        // Functions
        void SendMessage(byte[] data);

        void Close();

        // Events
        event Action OnClose;

        event Action<string> OnError;

        event Action<byte[]> OnMessage;

        event Action OnConnect;
    }
}