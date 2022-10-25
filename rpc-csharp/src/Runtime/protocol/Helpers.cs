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
        public static byte[] CloseStreamMessage(uint messageNumber, uint sequenceId, uint portId)
        {
            var streamMessage = new StreamMessage()
            {
                MessageIdentifier = CalculateMessageIdentifier(
                    RpcMessageTypes.StreamMessage,
                    messageNumber
                ),
                PortId = portId,
                SequenceId = sequenceId,
                Closed = true,
                Ack = false
            };

            return streamMessage.ToByteArray();
        }
        
        public static byte[] StreamAckMessage(uint messageNumber, uint sequenceId, uint portId)
        {
            var streamMessage = new StreamMessage()
            {
                MessageIdentifier = CalculateMessageIdentifier(
                    RpcMessageTypes.StreamAck,
                    messageNumber
                ),
                PortId = portId,
                SequenceId = sequenceId,
                Closed = false,
                Ack = true
            };

            return streamMessage.ToByteArray();
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

        public enum AsyncQueueActionType
        {
            Close,
            Next
        }
        public class AsyncQueue<T> : IUniTaskAsyncEnumerator<T> where T : class
        {
            private readonly RequestingNext requestingNext;
            private bool closed = false;
            private bool closing = false;
            private LinkedList<T> values = new LinkedList<T>();
            private LinkedList<(Action<(T, bool)>, Action<Exception>)> settlers = new LinkedList<(Action<(T, bool)>, Action<Exception>)>();
            private Exception error = null;
            private T current;
            public delegate void RequestingNext(AsyncQueue<T> queue, AsyncQueueActionType action);
            
            public AsyncQueue(RequestingNext requestingNext)
            {
                this.requestingNext = requestingNext;
            }

            public void Enqueue(T value)
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
                } else {
                    values.AddLast(value);
                }
            }
            public async UniTask DisposeAsync()
            {
                if (!closing && !closed)
                {
                    Close();
                }
            }

            public UniTask<bool> MoveNextAsync()
            {
                closing = true;
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

                if (closed)
                {
                    if (settlers.Count > 0)
                    {
                        throw new InvalidOperationException("Illegal internal state");
                    }
                    return UniTask.FromResult(false);
                }

                // Wait for new values to be enqueued
                var ret = new UniTaskCompletionSource<bool>();
                var accept = new Action<(T, bool)>(message =>
                {
                    if (message.Item2)
                    {
                        current = message.Item1;
                        ret.TrySetResult(true);
                    }
                    else
                    {
                        current = null;
                        ret.TrySetResult(false);
                    }
                });

                var reject = new Action<Exception>(error =>
                {
                    ret.TrySetException(error);
                });

                settlers.AddLast((accept, reject));
                requestingNext(this, AsyncQueueActionType.Next);
                return ret.Task;
            }
            
            public void Close(Exception error = null) {
                if (error != null)
                {
                    foreach (var settler in settlers)
                    {
                        settler.Item2(error);
                    }
                    settlers.Clear();
                }
                else
                {
                    foreach (var settler in settlers)
                    {
                        settler.Item1((null, false));
                    }
                    settlers.Clear();
                }

                if (error != null)
                {
                    this.error = error;
                }

                if (!closed)
                {
                    closed = true;
                    requestingNext(this, AsyncQueueActionType.Close);
                }
            }

            public T Current => current;
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