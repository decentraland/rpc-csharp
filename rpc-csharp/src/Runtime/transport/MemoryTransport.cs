using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace rpc_csharp.transport
{
    public class MemoryTransport : ITransport
    {
        private MemoryTransport sender;

        private MemoryTransport()
        {
        }

        public static (ITransport, ITransport) Create()
        {
            var client = new MemoryTransport();
            var server = new MemoryTransport();

            client.Attach(server);
            server.Attach(client);

            return (client, server);
        }

        public void Attach(MemoryTransport sender)
        {
            this.sender = sender;
        }

        public Action<byte[]> GetOnMessageEvent()
        {
            return OnMessageEvent;
        }

        public Action GetOnCloseEvent()
        {
            return OnCloseEvent;
        }

        public void SendMessage(byte[] data)
        {
            // Decouple
            UniTask.Create(() =>
            {
                sender.GetOnMessageEvent().Invoke(data);
                return UniTask.CompletedTask;
            });
        }

        public void Close()
        {
            sender.GetOnCloseEvent().Invoke();
        }
        public void Dispose()
        {
            OnCloseEvent = null;
            OnErrorEvent = null;
            OnMessageEvent = null;
            OnConnectEvent = null;
        }

        public event Action OnCloseEvent;
        public event Action<string> OnErrorEvent;
        public event Action<byte[]> OnMessageEvent;
        public event Action OnConnectEvent;
    }
}