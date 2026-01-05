using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace rpc_csharp.transport
{
    public interface ITransport : IDisposable
    {
        // Functions
        UniTask SendMessageAsync(byte[] data, CancellationToken ct);

        /// <summary>
        /// Send and Forget
        /// </summary>
        void SendMessage(byte[] data) => SendMessageAsync(data, CancellationToken.None).Forget();

        void Close();
        
        // Events
        event Action OnCloseEvent;

        event Action<Exception> OnErrorEvent;

        event Action<byte[]> OnMessageEvent;

        event Action OnConnectEvent;
    }
}