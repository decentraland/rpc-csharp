using System;
using ProtoBuf;
using rpc_csharp.protocol;
using rpc_csharp.transport;

namespace rpc_csharp
{
    public class RpcServer
    {
        private ITransport? transport;

        public RpcServer()
        {
        }

        public void AttachTransport(ITransport newTransport /*, context */)
        {
            transport = newTransport;

            transport.OnMessage += (byte[] data) =>
            {
                var parsedMessage = ProtocolHelpers.ParseProtocolMessage(data);

                if (parsedMessage != null)
                {
                    var (messageType, message, messageNumber) = parsedMessage.Value;
                    if (messageType == RpcMessageTypes.CreatePort)
                    {
                        var createPortMessage = (message as CreatePort)!;
                        
                        Console.WriteLine("PortName: " + createPortMessage.PortName);
                    }
                    //var key = $"{messageNumber},{header}";
                    Console.WriteLine("MessageNumber: " + messageNumber);
                }
            };
        }
    }
}