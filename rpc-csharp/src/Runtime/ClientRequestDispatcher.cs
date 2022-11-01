using System.Threading;
using Cysharp.Threading.Tasks;
using Google.Protobuf;
using rpc_csharp.protocol;
using rpc_csharp.transport;

namespace rpc_csharp
{
    public class ClientRequestDispatcher : MessageDispatcher
    {
        public delegate void SendMessage();

        private static uint globalMessageNumber = 0;
        
        public ClientRequestDispatcher(ITransport transport) : base(transport)
        {
        }

        public UniTask<ParsedMessage> AwaitForMessage(uint messageNumber, SendMessage sendMessage = null)
        {
            return UniTask.Create(() =>
            {
                var responseFuture = new UniTaskCompletionSource<ParsedMessage>();

                void OnMessage(ParsedMessage message)
                {
                    if (messageNumber != message.messageNumber) return;
                    
                    OnParsedMessage -= OnMessage;
                    responseFuture.TrySetResult(message);
                }

                OnParsedMessage += OnMessage;

                if (sendMessage != null)
                    sendMessage();

                return responseFuture.Task;
            });
        }

        public uint NextMessageNumber()
        {
            var messageNumber = ++globalMessageNumber;
            if (globalMessageNumber > 0x01000000) globalMessageNumber = 0;
            return messageNumber;
        }
    }
}