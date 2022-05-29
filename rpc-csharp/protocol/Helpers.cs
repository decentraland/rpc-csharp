using ProtoBuf;

namespace rpc_csharp.protocol;

public class ProtocolHelpers
{
    // @internal
    public static (RpcMessageTypes, uint) ParseMessageIdentifier(uint value)
    {
        return ((RpcMessageTypes)((value >> 27) & 0xf), value & 0x07ffffff);
    }

    // @internal
    public static uint CalculateMessageIdentifier(uint messageType, uint messageNumber)
    {
        return ((messageType & 0xf) << 27) | (messageNumber & 0x07ffffff);
    }
    
    public static (RpcMessageTypes, object, uint) ParseProtocolMessage(ProtoReader.State reader)
    {
        var header = reader.ReadMessage<RpcMessageHeader>();
        var (messageType, messageNumber) = ParseMessageIdentifier(header.MessageIdentifier);

        switch (messageType) {
            case RpcMessageTypes.CreatePortResponse:
                return (messageType, reader.ReadMessage<CreatePortResponse>(), messageNumber);
            case RpcMessageTypes.Response:
                return (messageType, reader.ReadMessage<Response>(), messageNumber);
            case RpcMessageTypes.RequestModuleResponse:
                return (messageType, reader.ReadMessage<RequestModuleResponse>(), messageNumber);
            case RpcMessageTypes.StreamMessage:
                return (messageType, reader.ReadMessage<StreamMessage>(), messageNumber);
            case RpcMessageTypes.ServerReady:
                throw new Exception("No exists");
            case RpcMessageTypes.RemoteErrorResponse:
                return (messageType, reader.ReadMessage<RemoteError>(), messageNumber);
            case RpcMessageTypes.Request:
                return (messageType, reader.ReadMessage<Request>(), messageNumber);
            case RpcMessageTypes.CreatePort:
                return (messageType, reader.ReadMessage<CreatePort>(), messageNumber);
            case RpcMessageTypes.StreamAck:
                return (messageType, reader.ReadMessage<StreamMessage>(), messageNumber);
            case RpcMessageTypes.RequestModule:
                return (messageType, reader.ReadMessage<RequestModule>(), messageNumber);
            case RpcMessageTypes.DestroyPort:
                return (messageType, reader.ReadMessage<DestroyPort>(), messageNumber);
        }

        throw new Exception("No exists");
    }

}