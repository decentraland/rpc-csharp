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

        private async Task SendStream(AckHelper ackHelper, ITransport transport, uint messageNumber, uint portId, IEnumerator<Task<byte[]>> stream)
        {
            uint sequenceNumber = 0;
            var reusedStreamMessage = new StreamMessage()
            {
                MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                    RpcMessageTypes.StreamMessage,
                    messageNumber
                ),
                Closed = false,
                Ack = false,
                Payload = ByteString.Empty,
                PortId = portId,
                SequenceId = sequenceNumber
            };
            
            // First, tell the client that we are opening a stream. Once the client sends
            // an ACK, we will know if they are ready to consume the first element.
            // If the response is instead close=true, then this function returns and
            // no stream.next() is called
            // The following lines are called "stream offer" in the tests.
            {
                var ret = await ackHelper.SendWithAck(reusedStreamMessage);
                if (ret.Closed) return;
                if (!ret.Ack) throw new Exception("Error in logic, ACK must be true");
            }

            // If this point is reached, then the client WANTS to consume an element of the
            // generator
            using (var iterator = stream)
            {
                while (iterator.MoveNext())
                {
                    var elem = await iterator.Current;
                    sequenceNumber++;
                    reusedStreamMessage.SequenceId = sequenceNumber;
                    reusedStreamMessage.Payload = ByteString.CopyFrom(elem); // TODO: OPTIMIZE!

                    var ret = await ackHelper.SendWithAck(reusedStreamMessage);
                    if (ret.Ack)
                    {
                        continue;
                    }
                    else if (ret.Closed)
                    {
                        break;
                    }
                }
            }

            transport.SendMessage(ProtocolHelpers.CloseStreamMessage(messageNumber, sequenceNumber, portId));
        }
        private async Task HandleRequest(Request message, uint messageNumber, Context context,
            ITransport transport, AckHelper ackHelper)
        {
            if (!ports.TryGetValue(message.PortId, out var port))
            {
                throw new InvalidOperationException($"Cannot find port {message.PortId}");
            }
            
            // TODO: CallStreamProcedure
            var obj = port.CallProcedure(message.ProcedureId, message.Payload.ToByteArray(), context);

            if (obj is Task<byte[]> unaryProcedure)
            {
                var res = await unaryProcedure;
                var response = new Response
                {
                    MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                        RpcMessageTypes.Response,
                        messageNumber
                    ),
                    Payload = ByteString.Empty
                };

                if (res.Length > 0)
                {
                    response.Payload = ByteString.CopyFrom(res);
                }
            
                transport.SendMessage(response.ToByteArray());
            }
            else if (obj is IEnumerator<Task<byte[]>> streamProcedure)
            {
                await SendStream(ackHelper, transport, messageNumber, port.portId, streamProcedure);
            }
            else
            {
                throw new InvalidOperationException($"Unknown type {message.PortId}");
            }
        }

        public void SetHandler(RpcServerHandler<Context> handler)
        {
            this.handler = handler;
        }

        public void AttachTransport(ITransport transport, Context context)
        {
            var ackHelper = new AckHelper(transport);
            
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
                        case RpcMessageTypes.Request:
                            await HandleRequest((Request)message, messageNumber, context, transport, ackHelper);
                            break;
                        case RpcMessageTypes.StreamAck:
                        case RpcMessageTypes.StreamMessage:
                            ackHelper.ReceiveAck((StreamMessage)message, messageNumber);
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