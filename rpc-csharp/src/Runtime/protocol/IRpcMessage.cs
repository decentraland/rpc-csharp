using Google.Protobuf;

public interface IRpcMessage : IMessage
{
    uint MessageIdentifier { get; set; }
}

public partial class CreatePort : IRpcMessage {}

public partial class CreatePortResponse : IRpcMessage {}

public partial class RequestModule : IRpcMessage {}

public partial class RequestModuleResponse : IRpcMessage {}

public partial class DestroyPort : IRpcMessage {}

public partial class Request : IRpcMessage {}

public partial class RemoteError : IRpcMessage {}

public partial class StreamMessage : IRpcMessage {}
    
    
