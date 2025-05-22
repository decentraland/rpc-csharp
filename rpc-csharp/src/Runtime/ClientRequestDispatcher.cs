using System;
using System.Collections.Concurrent;
using Cysharp.Threading.Tasks;
using Google.Protobuf;
using rpc_csharp.protocol;
using rpc_csharp.transport;

namespace rpc_csharp
{
    public class ClientRequestDispatcher : MessageDispatcher
    {
        // It doesn't feel right to preserve this number if the RPCClient has been recreated along with all its dependencies.
        private static uint globalMessageNumber = 0;
        
        private readonly ConcurrentDictionary<uint, UniTaskCompletionSource<ParsedMessage>> pendingMessages = new();
        
        public ClientRequestDispatcher(ITransport transport) : base(transport)
        {
        }
        
        protected override void OnMessageParsed(ParsedMessage parsedMessage)
        {
            if (pendingMessages.TryRemove(parsedMessage.messageNumber, out var promise))
                promise.TrySetResult(parsedMessage);
        }

        protected override void OnClose(Exception e)
        {
            // Reject regular messages
            foreach (var promise in pendingMessages.Values)
                promise.TrySetException(e);
            
            pendingMessages.Clear();
        }

        /// <summary>
        /// Should be used only for messages that don't expect a response
        /// </summary>
        public void SendAndForget(IRpcMessage request, RpcMessageTypes messageType)
        {
            var requestMessageNumber = NextMessageNumber();

            request.MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                messageType,
                requestMessageNumber
            );
            
            // TODO: find an opportunity to avoid such allocation
            var payload = request.ToByteArray();
            
            transport.SendMessage(payload);
        }

        public UniTask<TResponse> SendAndWaitForResponse<TResponse>(IRpcMessage request, RpcMessageTypes messageType) where TResponse : class, IMessage
        {
            var requestMessageNumber = NextMessageNumber();

            request.MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                messageType,
                requestMessageNumber
            );
            
            return SendAndWaitForResponse<TResponse>(request, requestMessageNumber);
        }

        public async UniTask<Response> HandleClientStream(Request negotiationMessage, uint portId, IUniTaskAsyncEnumerable<IMessage> stream)
        {
            var clientStreamMessageNumber = NextMessageNumber();
            var requestMessageNumber = NextMessageNumber();

            negotiationMessage.MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                RpcMessageTypes.Request,
                requestMessageNumber
            );
            
            negotiationMessage.ClientStream = clientStreamMessageNumber;
            
            
            
            // When the client stream has finished, Server will send the last element as an acknowledgment
            var lastElementAck = new UniTaskCompletionSource<ParsedMessage>();
            pendingMessages.TryAdd(requestMessageNumber, lastElementAck);
            
            await LaunchClientStreamAsync(negotiationMessage, portId, stream);
            
            // Wait for the last element or the error
            var content = await lastElementAck.Task;

            if (content.message == null)
                throw new Exception($"\"Null\" response was received at the end of the Client Stream, message number {requestMessageNumber}, stream number: {clientStreamMessageNumber}");

            if (content.message is not Response response)
                throw new Exception($"Invalid response type {content.message.GetType().Name} was received at the end of the Client Stream, message number {requestMessageNumber}, stream number: {clientStreamMessageNumber}");

            return response;
        }

        public async UniTask LaunchClientStreamAsync(Request negotiationMessage, uint portId, IUniTaskAsyncEnumerable<IMessage> stream)
        {
            // Server will send an ACK for the client stream
            var streamAckPromise = new UniTaskCompletionSource<ParsedMessage>();
            pendingMessages.TryAdd(negotiationMessage.ClientStream, streamAckPromise);
            
            transport.SendMessage(negotiationMessage.ToByteArray());

            await streamAckPromise.Task;
            
            // Start the stream
            StreamProtocol.SendStreamThroughTransportLoop(this, transport, negotiationMessage.ClientStream, portId, ProtocolHelpers.SerializeMessageEnumerator(stream)).Forget();
        }

        public (uint messageNumber, uint streamNumber) InitializeStreamNegotiation(Request negotiationMessage)
        {
            var clientStreamMessageNumber = NextMessageNumber();
            var requestMessageNumber = NextMessageNumber();
            
            negotiationMessage.MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                RpcMessageTypes.Request,
                requestMessageNumber
            );
            
            negotiationMessage.ClientStream = clientStreamMessageNumber;
            
            return (requestMessageNumber, clientStreamMessageNumber);
        }

        private async UniTask<TResponse> SendAndWaitForResponse<TResponse>(IRpcMessage request, uint requestMessageNumber)
            where TResponse : class, IMessage
        {
            var promise = new UniTaskCompletionSource<ParsedMessage>();
            pendingMessages.TryAdd(requestMessageNumber, promise);
            
            // TODO: find an opportunity to avoid such allocation
            var payload = request.ToByteArray();
            
            transport.SendMessage(payload);
            // Failure will be resolved in `OnClose`
            var parsedMessage = await promise.Task;

            if (parsedMessage.message == null)
                throw new Exception($"\"Null\" response was received for request {request.GetType().Name}, message number {requestMessageNumber}");

            if (parsedMessage.message is not TResponse response)
                throw new Exception($"Invalid response type {parsedMessage.message.GetType().Name} for request {request.GetType().Name}, message number {requestMessageNumber}");

            return response;
        }

        internal uint NextMessageNumber()
        {
            var messageNumber = ++globalMessageNumber;
            if (globalMessageNumber > 0x01000000) globalMessageNumber = 0;
            return messageNumber;
        }
    }
}