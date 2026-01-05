using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Google.Protobuf;
using rpc_csharp.protocol;

namespace rpc_csharp
{
    public class RpcClientModule
    {
        private readonly RpcClientPort port;
        private Dictionary<string, uint> procedures;

        internal RpcClientModule(RpcClientPort port, Dictionary<string, uint> procedures)
        {
            this.port = port;
            this.procedures = procedures;
        }
        
        private Request CreateRequestMessage(uint procedureId, IMessage payload)
        {
            return new Request
            {
                PortId = port.portId,
                ProcedureId = procedureId,
                Payload = payload.ToByteString() ?? ByteString.Empty,
            };
        }

        public async UniTask<T> CallUnaryProcedure<T>(string procedureName, IMessage payload)
            where T : IMessage, new()
        {
            if (!procedures.TryGetValue(procedureName, out uint procedureId))
            {
                throw new ArgumentException($"Procedure {procedureName} not found", nameof(procedureName));
            }
            
            var response = await port.dispatcher.SendAndWaitForResponse<Response>(CreateRequestMessage(procedureId, payload), RpcMessageTypes.Request);
            
            var ret = new T();
            return (T) ret.Descriptor.Parser.ParseFrom(response.Payload);
        }

        public IUniTaskAsyncEnumerable<T> CallServerStream<T>(string procedureName, IMessage payload)
            where T : IMessage, new()
        {
            if (!procedures.TryGetValue(procedureName, out uint procedureId))
            {
                throw (new Exception($"Procedure {procedureName} not found"));
            }

            var requestMessageNumber = port.dispatcher.NextMessageNumber();

            var streamFromDispatcherFuture = StreamProtocol.HandleServerStream(port.dispatcher, requestMessageNumber, port.portId);

            var requestMessage = CreateRequestMessage(procedureId, payload);
            requestMessage.MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                RpcMessageTypes.Request,
                requestMessageNumber
            );
            
            port.dispatcher.transport.SendMessage(requestMessage.ToByteArray());

            return ProtocolHelpers.DeserializeMessageEnumerator<T>(streamFromDispatcherFuture);
        }
        
        public async UniTask<T> CallClientStream<T>(string procedureName, IUniTaskAsyncEnumerable<IMessage> requestStream)
            where T : IMessage, new()
        {
            if (!procedures.TryGetValue(procedureName, out uint procedureId))
            {
                throw new Exception($"Procedure {procedureName} not found");
            }
            
            var parsedResponse = await port.dispatcher.HandleClientStream(ProtocolHelpers.ClientStreamRequestMessage(port.portId, procedureId), port.portId, requestStream);
            
            var ret = new T();
            ret.MergeFrom(parsedResponse.Payload);
            return ret;
        }
        
        public IUniTaskAsyncEnumerable<T> CallBidirectionalStream<T>(string procedureName, IUniTaskAsyncEnumerable<IMessage> requestStream)
            where T : IMessage, new()
        {
            if (!procedures.TryGetValue(procedureName, out uint procedureId))
            {
                throw new Exception($"Procedure {procedureName} not found");
            }

            var negotiationMessage = ProtocolHelpers.ClientStreamRequestMessage(port.portId, procedureId);
            var (messageNumber, streamNumber) = port.dispatcher.InitializeStreamNegotiation(negotiationMessage);

            var serverStreamAsyncEnumerable = StreamProtocol.HandleServerStream(port.dispatcher, messageNumber, port.portId);
            
            port.dispatcher.LaunchClientStreamAsync(negotiationMessage, port.portId, requestStream).Forget();
            
            return ProtocolHelpers.DeserializeMessageEnumerator<T>(serverStreamAsyncEnumerable);
        }
    }
}