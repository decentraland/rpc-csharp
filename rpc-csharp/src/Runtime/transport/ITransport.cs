using System;

namespace rpc_csharp.transport
{
    public interface ITransport : IDisposable
    {
        // Functions
        void SendMessage(byte[] data);

        void Close();
        
        // Events
        event Action OnCloseEvent;

        event Action<Exception> OnErrorEvent;

        event Action<byte[]> OnMessageEvent;

        event Action OnConnectEvent;
    }
}