using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Google.Protobuf;
using rpc_csharp.protocol;
using rpc_csharp.transport;

namespace rpc_csharp
{
    public class RpcClient
    {
        public ClientRequestDispatcher dispatcher { private set; get; }

        private Dictionary<string, RpcClientPort> portByName = new Dictionary<string, RpcClientPort>();

        private readonly ITransport transport;

        RpcClient(ITransport transport)
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
            return rpcClientPort;
        }
    }

    public class RpcClientPort
    {
        internal readonly string portName;
        internal readonly uint portId;
        public readonly ClientRequestDispatcher dispatcher;

        internal static async UniTask<RpcClientPort> CreatePort(ClientRequestDispatcher dispatcher, string portName)
        {
            var response = await dispatcher.Request((messageNumber) => new CreatePort
            {
                MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                    RpcMessageTypes.CreatePort,
                    messageNumber
                ),
                PortName = portName
            }.ToByteArray());
            
            var createPortResponse = response.message as CreatePortResponse;
            
            if (createPortResponse == null) throw new Exception("Invalid Create Port Response");
            
            return new RpcClientPort(dispatcher, portName, createPortResponse.PortId);
        }

        private RpcClientPort(ClientRequestDispatcher dispatcher, string portName, uint portId)
        {
            this.dispatcher = dispatcher;
            this.portName = portName;
            this.portId = portId;
        }

        private async UniTask<RpcClientModule> LoadModule(string serviceName)
        {
            var response = await dispatcher.Request((messageNumber) => new RequestModule
            {
                MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                    RpcMessageTypes.RequestModule,
                    messageNumber
                ),
                ModuleName = serviceName,
                PortId = portId
            }.ToByteArray());
            
            var requestModuleResponse = response.message as RequestModuleResponse;

            if (requestModuleResponse == null) throw new Exception("Invalid Request Module Response");

            var procedures = new Dictionary<string, uint>(requestModuleResponse.Procedures.Count);

            foreach (var moduleProcedure in requestModuleResponse.Procedures)
            {
                procedures.Add(moduleProcedure.ProcedureName, moduleProcedure.ProcedureId);
            }

            return new RpcClientModule(this, procedures);
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

        public async UniTask<T> CallProcedure<T>(string procedureName, IMessage args)
            where T : IMessage, new()
        {
            if (!procedures.TryGetValue(procedureName, out uint procedureId))
            {
                throw (new Exception($"Procedure {procedureName} not found"));
            }

            var response = await port.dispatcher.Request(messageNumber => new Request()
            {
                MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                    RpcMessageTypes.Request,
                    messageNumber
                ),
                PortId = port.portId,
                ProcedureId = procedureId,
                Payload = args.ToByteString()
            }.ToByteArray());
            
            var parsedResponse = response.message as Response;
            if (parsedResponse == null) throw new Exception("Invalid Response");
            var ret = new T();
            return (T) ret.Descriptor.Parser.ParseFrom(parsedResponse.Payload);
        }

        public async UniTask<IUniTaskAsyncEnumerable<T>> CallServerStream<T>(string procedureName, IMessage payload)
            where T : IMessage, new()
        {
            if (!procedures.TryGetValue(procedureName, out uint procedureId))
            {
                throw (new Exception($"Procedure {procedureName} not found"));
            }

            var response = await port.dispatcher.Request(messageNumber => new Request()
            {
                MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                    RpcMessageTypes.Request,
                    messageNumber
                ),
                PortId = port.portId,
                ProcedureId = procedureId,
                Payload = payload.ToByteString()
            }.ToByteArray());
            
            var streamFromDispatcherFuture = StreamProtocol.HandleServerStream(port.dispatcher, response.messageNumber, port.portId);

            return ProtocolHelpers.DeserializeMessageEnumerator(await streamFromDispatcherFuture, payload =>
            {
                var ret = new T();
                return (T) ret.Descriptor.Parser.ParseFrom(payload);
            });
        }
        
        public async UniTask<T> CallClientStream<T>(string procedureName, IUniTaskAsyncEnumerable<IMessage> requestStream)
            where T : IMessage, new()
        {
            if (!procedures.TryGetValue(procedureName, out uint procedureId))
            {
                throw (new Exception($"Procedure {procedureName} not found"));
            }

            var clientStreamMessageNumber = port.dispatcher.NextMessageNumber();

            var responseFuture = port.dispatcher.Request(messageNumber => new Request()
            {
                MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                    RpcMessageTypes.Request,
                    messageNumber
                ),
                PortId = port.portId,
                ProcedureId = procedureId
            }.ToByteArray());

            // No await! The stream is independent from the response
            StreamProtocol.HandleClientStream(port.dispatcher, clientStreamMessageNumber, port.portId,
                ProtocolHelpers.SerializeMessageEnumerator<IMessage>(requestStream));

            var parsedResponse = (await responseFuture).message as Response;
            if (parsedResponse == null) throw new Exception("Invalid Response");
            var ret = new T();
            return (T) ret.Descriptor.Parser.ParseFrom(parsedResponse.Payload);
        }
        
        public async UniTask<IUniTaskAsyncEnumerable<T>> CallBidirectionalStream<T>(string procedureName, IUniTaskAsyncEnumerable<IMessage> requestStream)
            where T : IMessage, new()
        {
            if (!procedures.TryGetValue(procedureName, out uint procedureId))
            {
                throw (new Exception($"Procedure {procedureName} not found"));
            }

            var clientStreamMessageNumber = port.dispatcher.NextMessageNumber();
            var requestMessageNumber = port.dispatcher.NextMessageNumber();

            var request = new Request()
            {
                MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                    RpcMessageTypes.Request,
                    requestMessageNumber
                ),
                PortId = port.portId,
                ProcedureId = procedureId,
                ClientStream = clientStreamMessageNumber,
                Payload = ByteString.Empty
            };

            var streamFromDispatcherFuture = StreamProtocol.HandleServerStream(port.dispatcher, requestMessageNumber, port.portId);
            
            // No await! The stream is independent from the response
            StreamProtocol.HandleClientStream(port.dispatcher, clientStreamMessageNumber, port.portId,
                ProtocolHelpers.SerializeMessageEnumerator<IMessage>(requestStream));
            
            port.dispatcher.transport.SendMessage(request.ToByteArray());

            return ProtocolHelpers.DeserializeMessageEnumerator(await streamFromDispatcherFuture, payload =>
            {
                var ret = new T();
                return (T) ret.Descriptor.Parser.ParseFrom(payload);
            });
        }
    }
}