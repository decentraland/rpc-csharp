using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.Collections;
using rpc_csharp.protocol;
using rpc_csharp.transport;

namespace rpc_csharp
{
    public class RpcServer<TContext> : IDisposable
    {
        private uint lastPortId = 0;

        internal readonly Dictionary<uint, RpcServerPort<TContext>> ports =
            new Dictionary<uint, RpcServerPort<TContext>>();

        private RpcServerHandler<TContext> handler;
        private readonly List<ITransport> transportList = new List<ITransport>();

        private readonly CancellationTokenSource cancellationTokenSource;
        private bool disposed = false;

        public RpcServer()
        {
            cancellationTokenSource = new CancellationTokenSource();
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;

            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();

            foreach (var port in ports.Values)
            {
                port.Close();
            }
            ports.Clear();

            foreach (var transport in transportList)
            {
                transport.Dispose();
            }
        }

        private RpcServerPort<TContext> HandleCreatePort(CreatePort message, uint messageNumber, TContext context,
            ITransport transport)
        {
            ++lastPortId;
            var port = new RpcServerPort<TContext>(lastPortId, message.PortName, cancellationTokenSource.Token);
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

        private void HandleDestroyPort(DestroyPort message)
        {
            if (!ports.TryGetValue(message.PortId, out var port))
            {
                throw new InvalidOperationException($"Cannot find port {message.PortId}");
            }

            port.Close();
            ports.Remove(message.PortId);
        }

        private async UniTask HandleRequestModule(RequestModule message, uint messageNumber, ITransport transport)
        {
            if (!ports.TryGetValue(message.PortId, out var port))
            {
                throw new InvalidOperationException($"Cannot find port {message.PortId}");
            }

            var loadedModule = await port.LoadModule(message.ModuleName);

            var inProcedures = loadedModule.procedures;

            var pbProcedures = new RepeatedField<ModuleProcedure>();

            int inProceduresCount = inProcedures.Count;
            for (int i = 0; i < inProceduresCount; i++)
            {
                var procedure = inProcedures[i];
                pbProcedures.Add(new ModuleProcedure()
                {
                    ProcedureId = procedure.procedureId,
                    ProcedureName = procedure.procedureName
                });
            }

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

        private async UniTask HandleRequest(Request message, uint messageNumber, TContext context,
            ITransport transport, MessageDispatcher messageDispatcher)
        {
            if (!ports.TryGetValue(message.PortId, out var port))
            {
                throw new InvalidOperationException($"Cannot find port {message.PortId}");
            }

            CallType procedureType = port.GetProcedureType(message.ProcedureId);
            if (procedureType == CallType.Unary)
            {
                var (unaryCallSuccess, unaryCallResult) =
                    await port.TryCallUnaryProcedure(message.ProcedureId, message.Payload, context);

                if (unaryCallSuccess)
                {
                    transport.SendMessage(ProtocolHelpers.CreateResponse(messageNumber, unaryCallResult));
                }
            }
            else if (procedureType == CallType.ClientStream)
            {
                var clientStream =
                    new StreamProtocol.StreamFromDispatcher(messageDispatcher, message.ClientStream, message.PortId);

                var result = await port.TryCallClientStreamProcedure(message.ProcedureId, clientStream, context);
                transport.SendMessage(ProtocolHelpers.CreateResponse(messageNumber, result));

                clientStream.CloseIfNotOpened();
                await clientStream.GetAsyncEnumerator().DisposeAsync();
            }
            else if (procedureType == CallType.ServerStream)
            {
                if (port.TryCallServerStreamProcedure(message.ProcedureId, message.Payload, context,
                        out var streamResult))
                {
                    await StreamProtocol.SendServerStream(messageDispatcher, transport, messageNumber, port.portId,
                        streamResult);
                }
            }
            else if (procedureType == CallType.BidirectionalStream)
            {
                IUniTaskAsyncEnumerable<ByteString> clientStream =
                    new StreamProtocol.StreamFromDispatcher(messageDispatcher, message.ClientStream, message.PortId);

                if (port.TryCallBidiStreamProcedure(message.ProcedureId, clientStream, context,
                        out var streamResult))
                {
                    await StreamProtocol.SendServerStream(messageDispatcher, transport, messageNumber, port.portId,
                        streamResult);
                }
            }
            else
            {
                throw new InvalidOperationException($"Unknown type {message.PortId}");
            }
        }

        public void SetHandler(RpcServerHandler<TContext> handler)
        {
            this.handler = handler;
        }

        public void AttachTransport(ITransport transport, TContext context)
        {
            var messageDispatcher = new MessageDispatcher(transport);
            
            transportList.Add(transport);
            
            messageDispatcher.OnParsedMessage += async (ParsedMessage parsedMessage) =>
            {
                if (disposed)
                    return;

                switch (parsedMessage.messageType)
                {
                    case RpcMessageTypes.CreatePort:
                        HandleCreatePort((CreatePort)parsedMessage.message, parsedMessage.messageNumber, context,
                            transport);
                        break;
                    case RpcMessageTypes.DestroyPort:
                        HandleDestroyPort((DestroyPort)parsedMessage.message);
                        break;
                    case RpcMessageTypes.RequestModule:
                        await HandleRequestModule((RequestModule)parsedMessage.message, parsedMessage.messageNumber,
                            transport);
                        break;
                    case RpcMessageTypes.Request:
                        await HandleRequest((Request)parsedMessage.message, parsedMessage.messageNumber, context,
                            transport, messageDispatcher);
                        break;
                    case RpcMessageTypes.StreamAck:
                    case RpcMessageTypes.StreamMessage:
                        // noop
                        break;
                    default:
                        Console.WriteLine("Not implemented message: " + parsedMessage.messageType);
                        break;
                }
            };
        }
    }
}