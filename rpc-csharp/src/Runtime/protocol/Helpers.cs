using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Google.Protobuf;
using rpc_csharp.transport;

namespace rpc_csharp.protocol
{
    public static class ProtocolHelpers
    {
        private static readonly StreamMessage reusableStreamMessage = new StreamMessage()
        {
            Closed = true,
            Ack = false,
            Payload = ByteString.Empty,
        };

        public static byte[] CloseStreamMessage(uint messageNumber, uint sequenceId, uint portId)
        {
            reusableStreamMessage.MessageIdentifier = CalculateMessageIdentifier(
                RpcMessageTypes.StreamMessage,
                messageNumber
            );
            reusableStreamMessage.PortId = portId;
            reusableStreamMessage.SequenceId = sequenceId;
            reusableStreamMessage.Closed = true;
            reusableStreamMessage.Ack = false;

            return reusableStreamMessage.ToByteArray();
        }
        
        public static byte[] StreamAckMessage(uint messageNumber, uint sequenceId, uint portId)
        {
            reusableStreamMessage.MessageIdentifier = CalculateMessageIdentifier(
                RpcMessageTypes.StreamMessage,
                messageNumber
            );
            reusableStreamMessage.PortId = portId;
            reusableStreamMessage.SequenceId = sequenceId;
            reusableStreamMessage.Closed = false;
            reusableStreamMessage.Ack = true;

            return reusableStreamMessage.ToByteArray();
        }

        // @internal
        public static (RpcMessageTypes, uint) ParseMessageIdentifier(uint value)
        {
            return ((RpcMessageTypes) ((value >> 27) & 0xf), value & 0x07ffffff);
        }

        // @internal
        public static uint CalculateMessageIdentifier(RpcMessageTypes messageType, uint messageNumber)
        {
            return (((uint) messageType & 0xf) << 27) | (messageNumber & 0x07ffffff);
        }

        public static (RpcMessageTypes, object, uint)? ParseProtocolMessage(byte[] data)
        {
            var header = RpcMessageHeader.Parser.ParseFrom(data);
            var (messageType, messageNumber) = ParseMessageIdentifier(header.MessageIdentifier);

            switch (messageType)
            {
                case RpcMessageTypes.CreatePortResponse:
                    return (messageType, CreatePortResponse.Parser.ParseFrom(data), messageNumber);
                case RpcMessageTypes.Response:
                    return (messageType, Response.Parser.ParseFrom(data), messageNumber);
                case RpcMessageTypes.RequestModuleResponse:
                    return (messageType, RequestModuleResponse.Parser.ParseFrom(data), messageNumber);
                case RpcMessageTypes.StreamMessage:
                    return (messageType, StreamMessage.Parser.ParseFrom(data), messageNumber);
                case RpcMessageTypes.ServerReady:
                    return null;
                case RpcMessageTypes.RemoteErrorResponse:
                    return (messageType, RemoteError.Parser.ParseFrom(data), messageNumber);
                case RpcMessageTypes.Request:
                    return (messageType, Request.Parser.ParseFrom(data), messageNumber);
                case RpcMessageTypes.CreatePort:
                    return (messageType, CreatePort.Parser.ParseFrom(data), messageNumber);
                case RpcMessageTypes.StreamAck:
                    return (messageType, StreamMessage.Parser.ParseFrom(data), messageNumber);
                case RpcMessageTypes.RequestModule:
                    return (messageType, RequestModule.Parser.ParseFrom(data), messageNumber);
                case RpcMessageTypes.DestroyPort:
                    return (messageType, DestroyPort.Parser.ParseFrom(data), messageNumber);
            }

            return null;
        }
        
        public static IUniTaskAsyncEnumerable<ByteString> SerializeMessageEnumerator<T>(IUniTaskAsyncEnumerable<T> generator) where T : IMessage
        {
            return UniTaskAsyncEnumerable.Create<ByteString>(async (writer, token) =>
            {
                await foreach (var current in generator)
                {
                    if (token.IsCancellationRequested)
                        break;
                    
                    if (current != null)
                        await writer.YieldAsync(current.ToByteString()); // instead of `yield return`
                }
            });
        }
        
        public delegate T Parser<out T>(ByteString payload);
        public static IUniTaskAsyncEnumerable<T> DeserializeMessageEnumerator<T>(IUniTaskAsyncEnumerable<ByteString> generator, Parser<T> parseFunc)
        {
            return UniTaskAsyncEnumerable.Create<T>(async (writer, token) =>
            {
                await foreach (var current in generator)
                {
                    if (token.IsCancellationRequested)
                        break;

                    if (current != null)
                    {
                        await writer.YieldAsync(parseFunc(current)); // instead of `yield return`
                    }
                }
            });
        }

        public class AsyncQueue : IUniTaskAsyncEnumerator<ByteString>
        {
            private readonly RequestingNext requestingNext;
            private bool closed = false;
            private LinkedList<ByteString> values = new LinkedList<ByteString>();
            private LinkedList<(Action<(ByteString, bool)>, Action<Exception>)> settlers = new LinkedList<(Action<(ByteString, bool)>, Action<Exception>)>();
            private Exception error = null;
            private ByteString current;
            public delegate void RequestingNext(AsyncQueue queue, string action);
            
            public AsyncQueue(RequestingNext requestingNext)
            {
                this.requestingNext = requestingNext;
            }

            public void Enqueue(ByteString value)
            {
                if (closed)
                {
                    throw new InvalidOperationException("Channel is closed");
                }
                if (settlers.Count > 0) {
                    if (values.Count > 0)
                    {
                        throw new InvalidOperationException("Illegal internal state");
                    }

                    var settler = settlers.First.Value;
                    settlers.RemoveFirst();
                    settler.Item1((value, true));
                } else
                {
                    values.AddLast(value);
                }
            }
            public async UniTask DisposeAsync()
            {
                Close();
            }

            public UniTask<bool> MoveNextAsync()
            {
                if (values.Count > 0)
                {
                    current = values.First.Value;
                    values.RemoveFirst();
                    return UniTask.FromResult(true);
                }
                if (error != null)
                {
                    throw error;
                }
                if (closed) {
                    if (settlers.Count > 0)
                    {
                        throw new InvalidOperationException("Illegal internal state");
                    }
                    return UniTask.FromResult(false);
                }
                // Wait for new values to be enqueued
                
                var ret = new UniTaskCompletionSource<bool>();
                var accept = new Action<(ByteString, bool)>(message =>
                {
                    current = message.Item1;
                    ret.TrySetResult(message.Item2);
                });
                var reject = new Action<Exception>(error =>
                {
                    ret.TrySetException(error);
                });
                //this.requestingNext(this, "next")
                requestingNext(this, "next");
                settlers.AddLast((accept, reject));
                return ret.Task;
            }
            
            public void Close(Exception error = null) {
                if (error != null)
                {
                    while (settlers.Count > 0)
                    {
                        settlers.First.Value.Item2(error);
                    }
                }
                else
                {
                    while (settlers.Count > 0)
                    {
                        settlers.First.Value.Item1((null, false));
                    }
                }

                if (error != null)
                {
                    this.error = error;
                }

                if (!closed)
                {
                    closed = true;
                    requestingNext(this, "close");
                }
            }

            public ByteString Current => current;
        }
        
        public class StreamEnumerator<T> : IUniTaskAsyncEnumerator<UniTask<ByteString>> where T : IMessage
        {
            private readonly IEnumerator<T> enumerator;

            private UniTaskCompletionSource<ByteString> messageFuture;
            private bool isDisposed = false;
            
            public StreamEnumerator(IEnumerator<T> enumerator)
            {
                this.enumerator = enumerator;
            }
            
            public async UniTask DisposeAsync()
            {
                isDisposed = true;
                enumerator.Dispose();
            }

            public UniTask<bool> MoveNextAsync()
            {
                return UniTask.Create(async () =>
                {
                    while (enumerator.MoveNext())
                    {
                        if (isDisposed)
                        {
                            return false;
                        }

                        var current = enumerator.Current;
                        if (current == null)
                        {
                            await UniTask.Yield();
                            continue;
                        }

                        messageFuture = new UniTaskCompletionSource<ByteString>();
                        messageFuture.TrySetResult(current.ToByteString());
                        return true;
                    }

                    return false;
                });
            }

            public UniTask<ByteString> Current => messageFuture.Task;
        }

        public static byte[] CreateResponse(uint messageNumber, ByteString payload)
        {
            var response = new Response
            {
                MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                    RpcMessageTypes.Response,
                    messageNumber
                ),
                Payload = payload ?? ByteString.Empty
            };

            return response.ToByteArray();
        }
    }
}