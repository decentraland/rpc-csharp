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
            
            var (messageType, message, messageNumber) = ProtocolHelpers.ParseProtocolMessage(reader);

            //var key = $"{messageNumber},{header}";
            
            Console.WriteLine("MessageNumber: " + messageNumber);
        };
    }
}