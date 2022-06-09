using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using rpc_csharp.transport;

namespace rpc_csharp.server
{
    public delegate void RpcServerHandler<Context>
    (
        IRpcServerPort<Context> serverPort,
        ITransport transport,
        Context context
    );

    public delegate UniTask<byte[]> UnaryCallback<Context>(byte[] payload, Context context);

    public delegate IEnumerator<UniTask<byte[]>> AsyncGenerator<Context>(byte[] payload, Context context);

    /*public class CallableProcedureServer<Context>
    {
        private readonly CallableProcedureServer<Context>? callable;
        private readonly AsyncGenerator<Context>? asyncCallable;

        CallableProcedureServer(CallableProcedureServer<Context>? callable)
        {
            this.callable = callable;
        }
        
        CallableProcedureServer(AsyncGenerator<Context>? asyncCallable)
        {
            this.asyncCallable = asyncCallable;
        }
        void Call()
        {
            if (callable != null)
            {
                this.callable?.Invoke();
            }
            else if (asyncCallable != null)
            {
                
            }
        }
    }*/

    public delegate UniTask<ServerModuleDefinition<Context>> ModuleGeneratorFunction<Context>(
        IRpcServerPort<Context> port);

    public class ServerModuleDefinition<Context>
    {
        public Dictionary<string, UnaryCallback<Context>> definition = new Dictionary<string, UnaryCallback<Context>>();

        public Dictionary<string, AsyncGenerator<Context>> streamDefinition =
            new Dictionary<string, AsyncGenerator<Context>>();
    }

    public class ServerModuleProcedure<Context>
    {
        public string procedureName;
        public uint procedureId;
        public UnaryCallback<Context> callable;
        public AsyncGenerator<Context> asyncCallable;
    }

    public class ServerModuleDeclaration<Context>
    {
        public List<ServerModuleProcedure<Context>> procedures;
    }

    public interface IRpcServerPort<Context>
    {
        event Action OnClose;
        uint portId { get; }
        string portName { get; }
        void RegisterModule(string moduleName, ModuleGeneratorFunction<Context> moduleDefinition);
        UniTask<ServerModuleDeclaration<Context>> LoadModule(string moduleName);
        IEnumerator<UniTask<byte[]>> CallStreamProcedure(uint procedureId, byte[] payload, Context context);
        UniTask<byte[]> CallUnaryProcedure(uint procedureId, byte[] payload, Context context);
        object CallProcedure(uint procedureId, byte[] payload, Context context);
        void Close();
    }
}