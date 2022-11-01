using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Google.Protobuf;
using rpc_csharp;
using rpc_csharp.protocol;
using rpc_csharp.transport;

namespace rpc_csharp_test
{
    public class TestClient
    {
        public Dictionary<string, uint> prodecures { private set; get; }
        public ITransport transport { private set; get; }
        
        public MessageDispatcher messageDispatcher { private set; get; }
        public uint port { private set; get; }

        private uint messageNumber = 3; // request is messageNumber=3

        public static async UniTask<TestClient> Create(ITransport transport, string serviceName)
        {
            uint port = await CreatePort(transport);
            var procedures = await LoadModule(transport, port, serviceName);
            var messageDispatcher = new MessageDispatcher(transport);
            return new TestClient
            {
                transport = transport,
                port = port,
                prodecures = procedures,
                messageDispatcher = messageDispatcher
            };
        }

        public async UniTask<T> CallProcedure<T>(string procedureName, IMessage args)
            where T : IMessage, new()
        {
            if (!prodecures.TryGetValue(procedureName, out uint procedureId))
            {
                throw (new Exception($"Procedure {procedureName} not found"));
            }

            var request = new Request()
            {
                MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                    RpcMessageTypes.Request,
                    messageNumber++
                ),
                PortId = port,
                ProcedureId = procedureId,
                Payload = args.ToByteString()
            };

            var response = await AwaitResponse(transport, request);
            var parsedResponse = Response.Parser.ParseFrom(response);
            var ret = new T();
            return (T) ret.Descriptor.Parser.ParseFrom(parsedResponse.Payload);
        }

        public async UniTask<IUniTaskAsyncEnumerable<T>> CallServerStream<T>(string procedureName, IMessage payload)
            where T : IMessage, new()
        {
            if (!prodecures.TryGetValue(procedureName, out uint procedureId))
            {
                throw (new Exception($"Procedure {procedureName} not found"));
            }

            var requestMessageNumber = messageNumber++;
            
            var request = new Request()
            {
                MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                    RpcMessageTypes.Request,
                    requestMessageNumber
                ),
                PortId = port,
                ProcedureId = procedureId,
                Payload = payload.ToByteString()
            };
            
            var streamFromDispatcherFuture = StreamProtocol.HandleServerStream(messageDispatcher, requestMessageNumber, port);
            
            transport.SendMessage(request.ToByteArray());

            return ProtocolHelpers.DeserializeMessageEnumerator(await streamFromDispatcherFuture, payload =>
            {
                var ret = new T();
                return (T) ret.Descriptor.Parser.ParseFrom(payload);
            });
        }
        
        public async UniTask<T> CallClientStream<T>(string procedureName, IUniTaskAsyncEnumerable<IMessage> requestStream)
            where T : IMessage, new()
        {
            if (!prodecures.TryGetValue(procedureName, out uint procedureId))
            {
                throw (new Exception($"Procedure {procedureName} not found"));
            }

            var clientStreamMessageNumber = messageNumber++;
            var requestMessageNumber = messageNumber++;
            
            var request = new Request()
            {
                MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                    RpcMessageTypes.Request,
                    requestMessageNumber
                ),
                PortId = port,
                ProcedureId = procedureId,
                ClientStream = clientStreamMessageNumber,
                Payload = ByteString.Empty
            };
            
            var responseFuture = AwaitResponse(transport, null, RpcMessageTypes.Response);
            
            // No await! The stream is independent from the response
            StreamProtocol.HandleClientStream(messageDispatcher, clientStreamMessageNumber, port,
                ProtocolHelpers.SerializeMessageEnumerator<IMessage>(requestStream));
            
            transport.SendMessage(request.ToByteArray());

            var response = await responseFuture;
            var parsedResponse = Response.Parser.ParseFrom(response);
            var ret = new T();
            return (T) ret.Descriptor.Parser.ParseFrom(parsedResponse.Payload);
        }
        
        public async UniTask<IUniTaskAsyncEnumerable<T>> CallBidirectionalStream<T>(string procedureName, IUniTaskAsyncEnumerable<IMessage> requestStream)
            where T : IMessage, new()
        {
            if (!prodecures.TryGetValue(procedureName, out uint procedureId))
            {
                throw (new Exception($"Procedure {procedureName} not found"));
            }

            var clientStreamMessageNumber = messageNumber++;
            var requestMessageNumber = messageNumber++;

            var request = new Request()
            {
                MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                    RpcMessageTypes.Request,
                    requestMessageNumber
                ),
                PortId = port,
                ProcedureId = procedureId,
                ClientStream = clientStreamMessageNumber,
                Payload = ByteString.Empty
            };

            var streamFromDispatcherFuture = StreamProtocol.HandleServerStream(messageDispatcher, requestMessageNumber, port);
            
            // No await! The stream is independent from the response
            StreamProtocol.HandleClientStream(messageDispatcher, clientStreamMessageNumber, port,
                ProtocolHelpers.SerializeMessageEnumerator<IMessage>(requestStream));
            
            transport.SendMessage(request.ToByteArray());

            return ProtocolHelpers.DeserializeMessageEnumerator(await streamFromDispatcherFuture, payload =>
            {
                var ret = new T();
                return (T) ret.Descriptor.Parser.ParseFrom(payload);
            });
        }

        private static UniTask<byte[]> AwaitResponse<T>(ITransport transport, T request)
            where T : IMessage
        {
            return AwaitResponse(transport, request.ToByteArray(), null);
        }

        private static UniTask<byte[]> AwaitResponse(ITransport transport, byte[] request)
        {
            return AwaitResponse(transport, request, null);
        }

        private static UniTask<byte[]> AwaitResponse(ITransport transport, byte[]? request,
            RpcMessageTypes? responseType, uint? messageNumber = null)
        {
            return UniTask.Create(() =>
            {
                UniTaskCompletionSource<byte[]> responseFuture = new UniTaskCompletionSource<byte[]>();

                void OnMessage(byte[] bytes)
                {
                    if (!responseType.HasValue)
                    {
                        transport.OnMessageEvent -= OnMessage;
                        responseFuture.TrySetResult(bytes);
                    }
                    else
                    {
                        var type = responseType.Value;
                        var header = RpcMessageHeader.Parser.ParseFrom(bytes);
                        var (msgType, msgNumber) = ProtocolHelpers.ParseMessageIdentifier(header.MessageIdentifier);
                        if (msgType == type && (messageNumber == null || messageNumber == msgNumber))
                        {
                            transport.OnMessageEvent -= OnMessage;
                            responseFuture.TrySetResult(bytes);
                        }
                    }
                }

                transport.OnMessageEvent += OnMessage;
                if (request != null)
                    transport.SendMessage(request);
                return responseFuture.Task;
            });
        }

        private static async UniTask<uint> CreatePort(ITransport transport)
        {
            var request = new CreatePort
            {
                MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                    RpcMessageTypes.CreatePort,
                    1
                ),
                PortName = "testing-port"
            };

            var bytes = await AwaitResponse(transport, request);
            var parsedResponse = CreatePortResponse.Parser.ParseFrom(bytes);
            return parsedResponse.PortId;
        }

        private static async UniTask<Dictionary<string, uint>> LoadModule(ITransport transport, uint port,
            string serviceName)
        {
            var request = new RequestModule()
            {
                MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                    RpcMessageTypes.RequestModule,
                    2
                ),
                ModuleName = serviceName,
                PortId = port
            };

            var bytes = await AwaitResponse(transport, request);
            var parsedResponse = RequestModuleResponse.Parser.ParseFrom(bytes);

            Dictionary<string, uint> procedures = new Dictionary<string, uint>(parsedResponse.Procedures.Count);

            for (int i = 0; i < parsedResponse.Procedures.Count; i++)
            {
                procedures.Add(parsedResponse.Procedures[i].ProcedureName, parsedResponse.Procedures[i].ProcedureId);
            }

            return procedures;
        }

        private TestClient()
        {
        }
    }
}