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
            
        private List<UniTaskCompletionSource<byte[]>> messages = new List<UniTaskCompletionSource<byte[]>>();

        public int GetMessagesCount()
        {
            return messages.Count;
        }

        public TransportAsyncWrapper(ITransport transport)
        {
            this.transport = transport;

            transport.OnMessageEvent += bytes =>
            {
                var task = new UniTaskCompletionSource<byte[]>();
                task.TrySetResult(bytes);
                messages.Add(task);
            };
        }

        public UniTask<byte[]> GetNextMessage()
        {
            if (messages.Count == 0)
            {
                // TODO: This can await for a new message
                throw new Exception("No messages");
            }
            else
            {
                var task = messages[0];
                messages.RemoveAt(0);
                return task.Task;                    
            }
        }
    }
}