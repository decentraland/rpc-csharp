using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Google.Protobuf;
using rpc_csharp.protocol;
using rpc_csharp.transport;

namespace rpc_csharp_test
{
    public class TestClient
    {
        public Dictionary<string, uint> prodecures { private set; get; }
        public ITransport transport { private set; get; }
        public uint port { private set; get; }

        private uint messageNumber = 2;

        public static async UniTask<TestClient> Create(ITransport transport, string serviceName)
        {
            uint port = await CreatePort(transport);
            var procedures = await LoadModule(transport, port, serviceName);
            return new TestClient
            {
                transport = transport,
                port = port,
                prodecures = procedures
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

        public IUniTaskAsyncEnumerable<UniTask<T>> CallStream<T>(string procedureName, IMessage request)
            where T : IMessage, new()
        {
            if (!prodecures.TryGetValue(procedureName, out uint procedureId))
            {
                throw (new Exception($"Procedure {procedureName} not found"));
            }

            return new StreamEnumerable<T>(procedureId, request, transport, port, messageNumber);
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

        private static UniTask<byte[]> AwaitResponse(ITransport transport, byte[] request,
            RpcMessageTypes? responseType)
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
                        var (msgType, _) = ProtocolHelpers.ParseMessageIdentifier(header.MessageIdentifier);
                        if (msgType == type)
                        {
                            transport.OnMessageEvent -= OnMessage;
                            responseFuture.TrySetResult(bytes);
                        }
                    }
                }

                transport.OnMessageEvent += OnMessage;
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
                    0
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
                    1
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

        private class StreamEnumerable<T> : IUniTaskAsyncEnumerable<UniTask<T>> where T : IMessage, new()
        {
            private readonly IMessage request;
            private readonly uint procedureId;
            private readonly ITransport transport;
            private readonly uint port;
            private readonly uint messageNumber;

            public StreamEnumerable(uint procedureId, IMessage request, ITransport transport, uint port,
                uint messageNumber)
            {
                this.request = request;
                this.procedureId = procedureId;
                this.transport = transport;
                this.port = port;
                this.messageNumber = messageNumber;
            }

            public IUniTaskAsyncEnumerator<UniTask<T>> GetAsyncEnumerator(
                CancellationToken cancellationToken = new CancellationToken())
            {
                return new StreamEnumerator<T>(procedureId, request, transport, port, messageNumber);
            }
        }

        private class StreamEnumerator<T> : IUniTaskAsyncEnumerator<UniTask<T>> where T : IMessage, new()
        {
            private readonly ITransport transport;
            private readonly uint messageNumber;

            private UniTaskCompletionSource<T> elementFuture;

            private byte[] currentRequest;
            private bool isDisposed = false;

            public StreamEnumerator(uint procedureId, IMessage request, ITransport transport, uint port,
                uint messageNumber)
            {
                this.transport = transport;
                this.messageNumber = messageNumber;

                currentRequest = new Request()
                {
                    MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                        RpcMessageTypes.Request,
                        messageNumber
                    ),
                    PortId = port,
                    Payload = request.ToByteString(),
                    ProcedureId = procedureId
                }.ToByteArray();
            }

            public async UniTask DisposeAsync()
            {
                isDisposed = true;
            }

            public UniTask<bool> MoveNextAsync()
            {
                ByteString currentPayload = ByteString.Empty;

                elementFuture = new UniTaskCompletionSource<T>();

                return UniTask.Create(async () =>
                {
                    while (currentPayload.IsEmpty)
                    {
                        var bytes = await AwaitResponse(transport, currentRequest);

                        if (isDisposed)
                        {
                            return false;
                        }

                        var response = StreamMessage.Parser.ParseFrom(bytes);
                        if (response.Closed)
                        {
                            return false;
                        }

                        currentPayload = response.Payload;

                        currentRequest = new StreamMessage()
                        {
                            MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                                RpcMessageTypes.StreamMessage,
                                messageNumber
                            ),
                            PortId = response.PortId,
                            Ack = true,
                            SequenceId = response.SequenceId
                        }.ToByteArray();

                        if (!currentPayload.IsEmpty)
                        {
                            var element = new T();
                            element = (T) element.Descriptor.Parser.ParseFrom(response.Payload);
                            elementFuture.TrySetResult(element);
                            return true;
                        }

                        UniTask.Yield();
                    }

                    return false;
                });
            }

            public UniTask<T> Current => elementFuture.Task;
        }
    }
}