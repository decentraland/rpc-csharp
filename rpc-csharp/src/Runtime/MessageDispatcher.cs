using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Google.Protobuf;
using rpc_csharp.protocol;
using rpc_csharp.transport;

namespace rpc_csharp
{
    public readonly struct ParsedMessage
    {
        public readonly RpcMessageTypes messageType;
        public readonly object message;
        public readonly uint messageNumber;

        public ParsedMessage(RpcMessageTypes messageType, object message, uint messageNumber)
        {
            this.messageType = messageType;
            this.message = message;
            this.messageNumber = messageNumber;
        }
    }

    public class MessageDispatcher : IDisposable
    {
        public static int ID = 0;
        public int id = 0;
        public event Action<ParsedMessage> OnParsedMessage;

        private readonly Dictionary<MessageKey, UniTaskCompletionSource<StreamMessage>> oneTimeCallbacks = new();

        public readonly ITransport transport;

        public MessageDispatcher(ITransport transport)
        {
            id = ++ID;
            this.transport = transport;

            transport.OnCloseEvent += OnTransportCloseEvent;
            transport.OnErrorEvent += OnTransportErrorEvent;
            transport.OnMessageEvent += OnTransportMessageEvent;
        }
        
        public void Dispose()
        {
            transport.OnCloseEvent -= OnTransportCloseEvent;
            transport.OnErrorEvent -= OnTransportErrorEvent;
            transport.OnMessageEvent -= OnTransportMessageEvent;
        }

        private void OnTransportMessageEvent(byte[] data)
        {
            var parsedMessage = ProtocolHelpers.ParseProtocolMessage(data);

            if (parsedMessage != null)
            {
                var (messageType, message, messageNumber) = parsedMessage.Value;
                OnParsedMessage?.Invoke(new ParsedMessage(messageType, message, messageNumber));

                if (messageType is RpcMessageTypes.StreamAck or RpcMessageTypes.StreamMessage)
                {
                    ReceiveAck((StreamMessage) message, messageNumber);
                }
            }
        }

        private void OnTransportErrorEvent(Exception err)
        {
            CloseAll(err);
        }

        private void OnTransportCloseEvent()
        {
            var err = new Exception("Transport closed while waiting the ACK");
            CloseAll(err);
        }

        private void CloseAll(Exception err)
        {
            using (var iterator = oneTimeCallbacks.Values.GetEnumerator())
            {
                while (iterator.MoveNext())
                {
                    // reject
                    iterator.Current.TrySetException(err);
                }
            }

            oneTimeCallbacks.Clear();
        }

        private void ReceiveAck(StreamMessage data, uint messageNumber)
        {
            var key = new MessageKey(messageNumber, data.SequenceId);
            if (oneTimeCallbacks.TryGetValue(key, out var fut))
            {
                oneTimeCallbacks.Remove(key);
                fut.TrySetResult(data);
            }
        }

        public UniTask<StreamMessage> SendStreamMessage(StreamMessage data)
        {
            var (_, messageNumber) = ProtocolHelpers.ParseMessageIdentifier(data.MessageIdentifier);
            var key = new MessageKey(messageNumber, data.SequenceId);
            
            var ret = new UniTaskCompletionSource<StreamMessage>();
            oneTimeCallbacks.Add(key, ret);

            transport.SendMessage(data.ToByteArray());

            return ret.Task;
        }
        
        internal readonly struct MessageKey : IEquatable<MessageKey>
        {
            internal readonly uint messageNumber;
            internal readonly uint sequenceId;

            public MessageKey(uint messageNumber, uint sequenceId)
            {
                this.messageNumber = messageNumber;
                this.sequenceId = sequenceId;
            }

            public bool Equals(MessageKey other)
            {
                return messageNumber == other.messageNumber && sequenceId == other.sequenceId;
            }

            public override bool Equals(object obj)
            {
                return obj is MessageKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((int)messageNumber * 397) ^ (int)sequenceId;
                }
            }
        }
    }
}