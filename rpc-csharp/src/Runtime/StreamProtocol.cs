using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Google.Protobuf;
using rpc_csharp.protocol;
using rpc_csharp.transport;

namespace rpc_csharp
{
    public class StreamProtocol
    {
        private static StreamMessage reusedStreamMessage = new StreamMessage()
        {
            Closed = false,
            Ack = false,
            Payload = ByteString.Empty
        };

        private static async UniTask SendStreamThroughTransport(MessageDispatcher messageDispatcher, ITransport transport, uint messageNumber,
            uint portId,
            IUniTaskAsyncEnumerable<ByteString> stream)
        {
            uint sequenceNumber = 0;

            // reset stream message
            reusedStreamMessage.MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                RpcMessageTypes.StreamMessage,
                messageNumber
            );
            reusedStreamMessage.Closed = false;
            reusedStreamMessage.Ack = false;
            reusedStreamMessage.Payload = ByteString.Empty;
            reusedStreamMessage.PortId = portId;
            reusedStreamMessage.SequenceId = sequenceNumber;

            // If this point is reached, then the client WANTS to consume an element of the
            // generator

            await foreach (var elem in stream)
            {
                sequenceNumber++;
                reusedStreamMessage.SequenceId = sequenceNumber;
                reusedStreamMessage.Payload = elem;

                var ret = await messageDispatcher.SendStreamMessage(reusedStreamMessage);
                if (ret.Ack)
                {
                    continue;
                }
                else if (ret.Closed)
                {
                    break;
                }
            }

            transport.SendMessage(ProtocolHelpers.CloseStreamMessage(messageNumber, sequenceNumber, portId));
        }

        public static async UniTask SendServerStream(MessageDispatcher messageDispatcher, ITransport transport, uint messageNumber,
            uint portId,
            IUniTaskAsyncEnumerable<ByteString> stream)
        {
            uint sequenceNumber = 0;

            // reset stream message
            reusedStreamMessage.MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                RpcMessageTypes.StreamMessage,
                messageNumber
            );
            reusedStreamMessage.Closed = false;
            reusedStreamMessage.Ack = false;
            reusedStreamMessage.Payload = ByteString.Empty;
            reusedStreamMessage.PortId = portId;
            reusedStreamMessage.SequenceId = sequenceNumber;

            // First, tell the client that we are opening a stream. Once the client sends
            // an ACK, we will know if they are ready to consume the first element.
            // If the response is instead close=true, then this function returns and
            // no stream.next() is called
            // The following lines are called "stream offer" in the tests.
            {
                var ret = await messageDispatcher.SendStreamMessage(reusedStreamMessage);
                if (ret.Closed) return;
                if (!ret.Ack) throw new Exception("Error in logic, ACK must be true");
            }

            await SendStreamThroughTransport(messageDispatcher, transport, messageNumber, portId, stream);
        }
        
        public class StreamFromDispatcher : IUniTaskAsyncEnumerable<ByteString>
        {
            private readonly MessageDispatcher dispatcher;
            private readonly uint messageNumber;
            private readonly uint portId;
            private bool wasOpen = false;
            private uint lastReceivedSequenceId = 0;
            private bool isRemoteClosed = false;
            private ProtocolHelpers.AsyncQueue channel;

            public StreamFromDispatcher(MessageDispatcher dispatcher, uint messageNumber, uint portId)
            {
                channel = new ProtocolHelpers.AsyncQueue(RequestingNext);
                this.dispatcher = dispatcher;
                this.messageNumber = messageNumber;
                this.portId = portId;

                dispatcher.OnParsedMessage += OnProcessMessage;

                dispatcher.transport.OnCloseEvent += () =>
                {
                    channel.Close(new InvalidOperationException("RPC Transport closed"));
                };

                dispatcher.transport.OnErrorEvent += s =>
                {
                    channel.Close(new InvalidOperationException("RPC Transport failed"));
                };
            }

            private void RequestingNext(ProtocolHelpers.AsyncQueue queue, string action)
            {
                if (action == "close") {
                    dispatcher.OnParsedMessage -= OnProcessMessage;
                }
                if (!isRemoteClosed) {
                    if (action == "close")
                    {
                        dispatcher.transport.SendMessage(
                            ProtocolHelpers.CloseStreamMessage(messageNumber, lastReceivedSequenceId, portId));
                    } else if (action == "next") {
                        // mark the stream as opened
                        wasOpen = true;
                        dispatcher.transport.SendMessage(
                            ProtocolHelpers.StreamAckMessage(messageNumber, lastReceivedSequenceId, portId));
                    }
                }
            }

            private void OnProcessMessage(ParsedMessage parsedMessage)
            {
                if (parsedMessage.messageNumber == messageNumber)
                {
                    if (parsedMessage.messageType == RpcMessageTypes.StreamMessage)
                    {
                        if (parsedMessage.message is StreamMessage streamMessage)
                        {
                            if (streamMessage.Closed)
                            {
                                channel.Close();
                            }
                            else
                            {
                                channel.Enqueue(streamMessage.Payload);
                            }
                        }
                    }
                    else if (parsedMessage.messageType == RpcMessageTypes.RemoteErrorResponse)
                    {
                        isRemoteClosed = true;
                        channel.Close(new InvalidOperationException("RemoteError: " +
                                                                    ((parsedMessage.message as RemoteError)
                                                                        ?.ErrorMessage ?? "Unknown remote error")));
                    } else
                    {
                        channel.Close(new InvalidOperationException("RemoteError: Protocol error, unkown message"));
                    }
                }
            }

            public IUniTaskAsyncEnumerator<ByteString> GetAsyncEnumerator(CancellationToken cancellationToken = new CancellationToken())
            {
                return channel;
            }
        }
    }
}