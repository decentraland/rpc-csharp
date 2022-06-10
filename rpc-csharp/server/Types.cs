using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Google.Protobuf;
using rpc_csharp.transport;

namespace rpc_csharp.server
{
    public delegate void RpcServerHandler<TContext>
    (
        RpcServerPort<TContext> serverPort,
        ITransport transport,
        TContext context
    );

    public delegate UniTask<ByteString> UnaryCallback<in TContext>(ByteString payload, TContext context);

    public delegate UniTask<IEnumerator<ByteString>> AsyncGenerator<in TContext>(ByteString payload,
        TContext context);

    public delegate UniTask<ServerModuleDefinition<TContext>> ModuleGeneratorFunction<TContext>(
        RpcServerPort<TContext> port);

    public class ServerModuleDefinition<TContext>
    {
        public readonly Dictionary<string, UnaryCallback<TContext>> definition =
            new Dictionary<string, UnaryCallback<TContext>>();

        public readonly Dictionary<string, AsyncGenerator<TContext>> streamDefinition =
            new Dictionary<string, AsyncGenerator<TContext>>();
    }

    public class ServerModuleProcedure<TContext>
    {
        public string procedureName;
        public uint procedureId;
        public UnaryCallback<TContext> callable;
        public AsyncGenerator<TContext> asyncCallable;
    }

    public class ServerModuleDeclaration<TContext>
    {
        public List<ServerModuleProcedure<TContext>> procedures;
    }
}