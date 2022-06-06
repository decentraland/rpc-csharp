using Google.Protobuf;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using rpc_csharp.protocol;
using rpc_csharp.server;
using rpc_csharp.transport;

namespace rpc_csharp
{
    public class RpcServer<Context>
    {
        private uint lastPortId = 0;

        private Dictionary<uint, IRpcServerPort<Context>> ports = new();

        private RpcServerHandler<Context>? handler;

        public RpcServer()
        {
        }

        private IRpcServerPort<Context> HandleCreatePort(CreatePort message, uint messageNumber, Context context, ITransport transport)
        {
            ++lastPortId;
            var port = RpcServerPort<Context>.CreateServerPort(lastPortId, message.PortName);
            ports.Add(port.portId, port);

            handler?.Invoke(port, transport, context);

            var response = new CreatePortResponse
            {
                MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                    RpcMessageTypes.CreatePortResponse,
                    messageNumber
                ),
                PortId = port.portId
            };
            transport.SendMessage(response.ToByteArray());

            return port;
        }

        private async Task HandleRequestModule(RequestModule message, uint messageNumber, Context context, ITransport transport)
        {
            if (!ports.TryGetValue(message.PortId, out var port))
            {
                throw new InvalidOperationException($"Cannot find port {message.PortId}");
            }

            var loadedModule = await port.LoadModule(message.ModuleName);

            var pbProcedures = loadedModule.procedures.Select(x => new ModuleProcedure()
            {
                ProcedureId = x.procedureId,
                ProcedureName = x.procedureName
            }).ToList();

            var response = new RequestModuleResponse
            {
                Procedures = { pbProcedures },
                MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                    RpcMessageTypes.RequestModuleResponse,
                    messageNumber
                ),
                PortId = port.portId
            };
            transport.SendMessage(response.ToByteArray());
        }

        public void SetHandler(RpcServerHandler<Context> handler)
        {
            this.handler = handler;
        }

        public void AttachTransport(ITransport transport, Context context)
        {
            transport.OnMessageEvent += async (byte[] data) =>
            {
                var parsedMessage = ProtocolHelpers.ParseProtocolMessage(data);

                if (parsedMessage != null)
                {
                    var (messageType, message, messageNumber) = parsedMessage.Value;
                    switch (messageType)
                    {
                        case RpcMessageTypes.CreatePort:
                            HandleCreatePort((CreatePort)message, messageNumber, context, transport);
                            break;
                        case RpcMessageTypes.RequestModule:
                            await HandleRequestModule((RequestModule)message, messageNumber, context, transport);
                            break;
                        default:
                            Console.WriteLine("Not implemented message: " + messageType);
                            break;
                    }
                    //var key = $"{messageNumber},{header}";
                    Console.WriteLine("MessageNumber: " + messageNumber);
                }
            };
        }
    }
}