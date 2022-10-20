using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Google.Protobuf;
using rpc_csharp.transport;

namespace rpc_csharp
{
    public delegate void RpcServerHandler<TContext>
    (
        RpcServerPort<TContext> serverPort,
        ITransport transport,
        TContext context
    );

    public enum CallType
    {
        Unknown,
        Unary,
        ServerStream,
        ClientStream,
        BidirectionalStream
    }
    public delegate UniTask<ByteString> UnaryCallback<in TContext>(ByteString payload, TContext context,
        CancellationToken ct);

    public delegate IUniTaskAsyncEnumerable<ByteString> ServerStreamCallback<in TContext>(ByteString payload,
        TContext context);
    
    public delegate UniTask<ByteString> ClientStreamCallback<in TContext>(IUniTaskAsyncEnumerable<ByteString> payload,
        TContext context);
    
    public delegate IUniTaskAsyncEnumerable<ByteString> BidirectionalStreamCallback<in TContext>(IUniTaskAsyncEnumerable<ByteString> payload,
        TContext context);

    public delegate UniTask<ServerModuleDefinition<TContext>> ModuleGeneratorFunction<TContext>(
        RpcServerPort<TContext> port);

    public class ServerModuleDefinition<TContext>
    {
        public readonly Dictionary<string, UnaryCallback<TContext>> definition =
            new Dictionary<string, UnaryCallback<TContext>>();

        public readonly Dictionary<string, ServerStreamCallback<TContext>> serverStreamDefinition =
            new Dictionary<string, ServerStreamCallback<TContext>>();
        
        public readonly Dictionary<string, ClientStreamCallback<TContext>> clientStreamDefinition =
            new Dictionary<string, ClientStreamCallback<TContext>>();
        
        public readonly Dictionary<string, BidirectionalStreamCallback<TContext>> bidirectionalStreamDefinition =
            new Dictionary<string, BidirectionalStreamCallback<TContext>>();
    }

    public readonly struct ServerModuleProcedureInfo
    {
        public readonly string procedureName;
        public readonly uint procedureId;

        public ServerModuleProcedureInfo(uint procedureId, string procedureName)
        {
            this.procedureId = procedureId;
            this.procedureName = procedureName;
        }
    }

    public class ServerModuleDeclaration
    {
        public List<ServerModuleProcedureInfo> procedures;
    }
}