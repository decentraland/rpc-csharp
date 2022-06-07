using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Protobuf.Collections;
using rpc_csharp.transport;

namespace rpc_csharp.server
{
        
    public delegate void RpcServerHandler<Context>
    (
        IRpcServerPort<Context> serverPort,
        ITransport transport,
        Context context
    );
    public delegate Task<byte[]> UnaryCallback<Context>(byte[] payload, Context context);
    
    public delegate IEnumerator<Task<byte[]>> AsyncGenerator<Context>(byte[] payload, Context context);

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

    public delegate Task<ServerModuleDefinition<Context>> ModuleGeneratorFunction<Context>(
        IRpcServerPort<Context> port);

    public class ServerModuleDefinition<Context>
    {
        public Dictionary<string, UnaryCallback<Context>> definition { get; }
        public Dictionary<string, AsyncGenerator<Context>> streamDefinition { get; }
    }

    public class ServerModuleProcedure<Context>
    {
        string procedureName { get; }
        uint procedureId { get; }
        UnaryCallback<Context>? callable { get; }
        AsyncGenerator<Context>? asyncCallable { get; }
    }

    public class ServerModuleDeclaration<Context>
    {
        public List<ServerModuleProcedure<Context>> procedures { get; }
    }

    public interface IRpcServerPort<Context>
    {
        event Action OnClose;
        uint portId { get; }
        string portName { get; }
        void RegisterModule(string moduleName, ModuleGeneratorFunction<Context> moduleDefinition);
        Task<ServerModuleDeclaration<Context>> LoadModule(string moduleName);
        AsyncGenerator<Context> CallStreamProcedure(int procedureId, byte[] payload, Context context);
        UnaryCallback<Context> CallUnaryProcedure(int procedureId, byte[] payload, Context context);
        void Close();
    }
}