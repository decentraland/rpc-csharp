using System;
using System.Collections.Concurrent;
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
        public readonly IMessage message;
        public readonly uint messageNumber;

        public ParsedMessage(RpcMessageTypes messageType, IMessage message, uint messageNumber)
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

        private readonly ConcurrentDictionary<StreamMessageKey, UniTaskCompletionSource<StreamMessage>> pendingStreams = new();

        internal readonly ITransport transport;

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

                if (messageType is RpcMessageTypes.StreamAck or RpcMessageTypes.StreamMessage)
                {
                    ReceiveStreamAck((StreamMessage) message, messageNumber);
                }

                var result = new ParsedMessage(messageType, message, messageNumber);
                OnMessageParsed(result);
                OnParsedMessage?.Invoke(result);
            }
        }
        
        protected virtual void OnMessageParsed(ParsedMessage parsedMessage) { }
        
        protected virtual void OnClose(Exception e) { }

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
            // Reject streams
            
            foreach (var promise in pendingStreams.Values)
                promise.TrySetException(err);

            pendingStreams.Clear();
            
            OnClose(err);
        }

        private void ReceiveStreamAck(StreamMessage data, uint messageNumber)
        {
            var key = new StreamMessageKey(messageNumber, data.SequenceId);
            if (pendingStreams.TryRemove(key, out var fut))
                fut.TrySetResult(data);
        }

        public UniTask<StreamMessage> SendStreamMessage(StreamMessage data)
        {
            var (_, messageNumber) = ProtocolHelpers.ParseMessageIdentifier(data.MessageIdentifier);
            var key = new StreamMessageKey(messageNumber, data.SequenceId);
            
            var ret = new UniTaskCompletionSource<StreamMessage>();
            pendingStreams.TryAdd(key, ret);

            transport.SendMessage(data.ToByteArray());

            return ret.Task;
        }
        
        internal readonly struct StreamMessageKey : IEquatable<StreamMessageKey>
        {
            internal readonly uint messageNumber;
            internal readonly uint sequenceId;

            public StreamMessageKey(uint messageNumber, uint sequenceId)
            {
                this.messageNumber = messageNumber;
                this.sequenceId = sequenceId;
            }

            public bool Equals(StreamMessageKey other)
            {
                return messageNumber == other.messageNumber && sequenceId == other.sequenceId;
            }

            public override bool Equals(object obj)
            {
                return obj is StreamMessageKey other && Equals(other);
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