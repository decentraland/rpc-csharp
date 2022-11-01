using Cysharp.Threading.Tasks;
using Google.Protobuf;
using rpc_csharp.protocol;
using rpc_csharp.transport;

namespace rpc_csharp
{
    public class ClientRequestDispatcher : MessageDispatcher
    {
        public delegate byte[] RequestCall(uint messageNumber);

        private static uint globalMessageNumber = 0;
        
        public ClientRequestDispatcher(ITransport transport) : base(transport)
        {
        }

        public UniTask<ParsedMessage> Request(RequestCall cb)
        {
            var messageNumber = NextMessageNumber();
            var requestBytes = cb.Invoke(messageNumber);
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
                if (requestBytes != null)
                    transport.SendMessage(requestBytes);

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