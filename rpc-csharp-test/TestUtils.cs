using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Google.Protobuf;
using rpc_csharp.protocol;
using rpc_csharp.transport;

namespace rpc_csharp_test
{
    public static class TestUtils
    {
        public static void ClientSendStreamMessageAck(ITransport client, uint messageNumber, uint portId, uint sequenceId)
        {
            var request = new StreamMessage
            {
                MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                    RpcMessageTypes.StreamMessage,
                    messageNumber
                ),
                PortId = portId,
                Ack = true,
                SequenceId = sequenceId
            };
            client.SendMessage(request.ToByteArray());
        }
    }
    public class TransportAsyncWrapper
    {
        private ITransport transport;
            
        private List<byte[]> messages = new List<byte[]>();
        private UniTaskCompletionSource<byte[]> nextMessage;
        private bool waitingForTask = false;

        public int GetMessagesCount()
        {
            return messages.Count;
        }

        public TransportAsyncWrapper(ITransport transport)
        {
            this.transport = transport;

            transport.OnMessageEvent += bytes =>
            {
                if (waitingForTask)
                {
                    waitingForTask = false;
                    nextMessage.TrySetResult(bytes);
                }
                else
                {
                    messages.Add(bytes);
                }
            };
        }

        public UniTask<byte[]> GetNextMessage()
        {
            if (messages.Count == 0)
            {
                if (waitingForTask == true)
                {
                    throw new Exception("Double waiting not supported");
                }
                nextMessage = new UniTaskCompletionSource<byte[]>();
                waitingForTask = true;
                return nextMessage.Task;
            }

            var message = messages[0];
            messages.RemoveAt(0);
            return UniTask.FromResult(message);
        }
    }
}