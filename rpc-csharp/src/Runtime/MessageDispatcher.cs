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
        public int _id = 0;
        public event Action<ParsedMessage> OnParsedMessage;

        private readonly Dictionary<string, (Action<StreamMessage>, Action<Exception>)> oneTimeCallbacks =
            new Dictionary<string, (Action<StreamMessage>, Action<Exception>)>();

        public readonly ITransport transport;

        public MessageDispatcher(ITransport transport)
        {
            _id = ++ID;
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

                if (messageType == RpcMessageTypes.StreamAck || messageType == RpcMessageTypes.StreamMessage)
                {
                    ReceiveAck((StreamMessage) message, messageNumber);
                }
            }
        }

        private void OnTransportErrorEvent(string err)
        {
            CloseAll(new Exception(err));
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
                    iterator.Current.Item2(err);
                }
            }

            oneTimeCallbacks.Clear();
        }

        private void ReceiveAck(StreamMessage data, uint messageNumber)
        {
            var key = $"{messageNumber},{data.SequenceId}";
            if (oneTimeCallbacks.TryGetValue(key, out var fut))
            {
                oneTimeCallbacks.Remove(key);
                fut.Item1(data);
            }
        }

        public UniTask<StreamMessage> SendStreamMessage(StreamMessage data)
        {
            var (_, messageNumber) = ProtocolHelpers.ParseMessageIdentifier(data.MessageIdentifier);
            var key = $"{messageNumber},{data.SequenceId}";

            // C# Promiches
            var ret = new UniTaskCompletionSource<StreamMessage>();
            var accept = new Action<StreamMessage>(message => { ret.TrySetResult(message); });
            var reject = new Action<Exception>(error => { ret.TrySetException(error); });
            oneTimeCallbacks.Add(key, (accept, reject));

            transport.SendMessage(data.ToByteArray());

            return ret.Task;
        }
    }
}