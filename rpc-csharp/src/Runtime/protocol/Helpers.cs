using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Google.Protobuf;

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

        public static IEnumerator<UniTask<ByteString>> SerializeMessageEnumerator<T>(IEnumerator<UniTask<T>> generator)
            where T : IMessage
        {
            using (var iterator = generator)
            {
                while (iterator.MoveNext())
                {
                    var current = iterator.Current;
                    yield return current.ContinueWith(m => m.ToByteString());
                }
            }
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
    }
}