using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Google.Protobuf;
using rpc_csharp.protocol;
using rpc_csharp.transport;

namespace rpc_csharp
{
    public class RpcClient : IDisposable
    {
        public ClientRequestDispatcher dispatcher { private set; get; }

        private Dictionary<string, RpcClientPort> portByName = new Dictionary<string, RpcClientPort>();

        private readonly ITransport transport;

        public RpcClient(ITransport transport)
        {
            this.transport = transport;
            dispatcher = new ClientRequestDispatcher(transport);
        }

        public async UniTask<RpcClientPort> CreatePort(string portName)
        {
            RpcClientPort rpcClientPort;
            
            if (portByName.TryGetValue(portName, out rpcClientPort))
            {
                return rpcClientPort;
            }

            rpcClientPort = await RpcClientPort.CreatePort(dispatcher, portName);
            portByName.Add(portName, rpcClientPort);

            rpcClientPort.OnPortClosed += OnPortClosed;
            
            return rpcClientPort;
        }

        private void OnPortClosed(string portName)
        {
            portByName.Remove(portName);
        }

        public void Dispose()
        {
            // the ports should be removed in the server when it disconnect
            // no need to send the message to the server
            portByName.Clear();
            
            dispatcher.Dispose();
        }
    }

    public class RpcClientPort
    {
        internal readonly string portName;
        internal readonly uint portId;
        internal readonly ClientRequestDispatcher dispatcher;
        public Action<string> OnPortClosed;

        internal static async UniTask<RpcClientPort> CreatePort(ClientRequestDispatcher dispatcher, string portName)
        {
            var createPortResponse = await dispatcher.SendAndWaitForResponse<CreatePortResponse>(new CreatePort
            {
                PortName = portName
            }, RpcMessageTypes.CreatePort);
            
            return new RpcClientPort(dispatcher, portName, createPortResponse.PortId);
        }

        private RpcClientPort(ClientRequestDispatcher dispatcher, string portName, uint portId)
        {
            this.dispatcher = dispatcher;
            this.portName = portName;
            this.portId = portId;
        }

        public async UniTask<RpcClientModule> LoadModule(string serviceName)
        {
            var requestModuleResponse = await dispatcher.SendAndWaitForResponse<RequestModuleResponse>(new RequestModule
            {
                ModuleName = serviceName,
                PortId = portId
            }, RpcMessageTypes.RequestModule);

            var procedures = new Dictionary<string, uint>(requestModuleResponse.Procedures.Count);

            foreach (var moduleProcedure in requestModuleResponse.Procedures)
            {
                procedures.Add(moduleProcedure.ProcedureName, moduleProcedure.ProcedureId);
            }

            return new RpcClientModule(this, procedures);
        }
        
        public void Close()
        {
            dispatcher.SendAndForget(new DestroyPort()
            {
                PortId = portId
            }, RpcMessageTypes.DestroyPort);
            
            // Warning: we don't expect a response, we close the port immediately from the client perspective
            OnPortClosed?.Invoke(portName);
        }
    }

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