using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Google.Protobuf;
using rpc_csharp.protocol;
using rpc_csharp.transport;

namespace rpc_csharp
{
    public struct ParsedMessage
    {
        public RpcMessageTypes messageType;
        public object message;
        public uint messageNumber;

        public ParsedMessage(RpcMessageTypes messageType, object message, uint messageNumber)
        {
            this.messageType = messageType;
            this.message = message;
            this.messageNumber = messageNumber;
        }
    }
    public class MessageDispatcher
    {
        public event Action<ParsedMessage> OnParsedMessage;
        
        private readonly Dictionary<string, (Action<StreamMessage>, Action<Exception>)> oneTimeCallbacks =
            new Dictionary<string, (Action<StreamMessage>, Action<Exception>)>();

        public readonly ITransport transport;

        public MessageDispatcher(ITransport transport)
        {
            this.transport = transport;

            transport.OnCloseEvent += () =>
            {
                var err = new Exception("Transport closed while waiting the ACK");
                CloseAll(err);
            };

            transport.OnErrorEvent += (err) => { CloseAll(new Exception(err)); };

            transport.OnMessageEvent += data =>
            {
                var parsedMessage = ProtocolHelpers.ParseProtocolMessage(data);

                if (parsedMessage != null)
                {
                    var (messageType, message, messageNumber) = parsedMessage.Value;
                    OnParsedMessage?.Invoke(new ParsedMessage(messageType, message, messageNumber));
                }
            };
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

        public void ReceiveAck(StreamMessage data, uint messageNumber)
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
            var accept = new Action<StreamMessage>(message =>
            {
                ret.TrySetResult(message);
            });
            var reject = new Action<Exception>(error =>
            {
                ret.TrySetException(error);
            });
            oneTimeCallbacks.Add(key, (accept, reject));

            transport.SendMessage(data.ToByteArray());

            return ret.Task;
        }
    }
}