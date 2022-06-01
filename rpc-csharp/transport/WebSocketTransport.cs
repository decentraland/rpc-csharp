using System;

namespace rpc_csharp.transport
{
    public sealed class WebSocketTransport : ITransport, WebSocketEvents
    {
        private WebSocketService service;
        public event Action? OnClose;
        public event Action<string>? OnError;
        public event Action<byte[]>? OnMessage;
        public event Action? OnConnect;

        public WebSocketTransport()
        {
            service = new WebSocketService(this);
        }

        public WebSocketService GetService()
        {
            return service;
        }

        public void SendMessage(byte[] data)
        {
            service.SendMessage(data);
        }

        public void Close()
        {
            service.Close();
        }

        public void OnConnectHandler()
        {
            OnConnect?.Invoke();
        }

        public void OnMessageHandler(byte[] data)
        {
            OnMessage?.Invoke(data);
        }

        public void OnErrorHandler(string error)
        {
            OnError?.Invoke(error);
        }

        public void OnCloseHandler()
        {
            OnClose?.Invoke();
        }
    }
}