using ProtoBuf;
using rpc_csharp.protocol;
using rpc_csharp.transport;

namespace rpc_csharp;

public class RpcServer
{
    private ITransport? transport;
    
    public RpcServer()
    {
        
    }

    public void AttachTransport(ITransport newTransport/*, context */)
    {
        transport = newTransport;
        
        transport.OnMessage += (byte[] data) =>
        {
            var reader = ProtoReader.State.Create(data, null);
            
            var parsedMessage = ProtocolHelpers.ParseProtocolMessage(reader);

            if (parsedMessage != null)
            {
                var (messageType, message, messageNumber) = parsedMessage.Value;
            }

            //var key = $"{messageNumber},{header}";
            
            Console.WriteLine("MessageNumber: " + messageNumber);
        };
    }
}