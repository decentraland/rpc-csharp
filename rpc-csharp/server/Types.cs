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
    public delegate Task<byte[]> CallableProcedureServer<Context>(byte[] payload, Context context);

    public delegate Task<byte[]> AsyncProcedureResultServer();

    public delegate Task<IServerModuleDefinition<Context>> ModuleGeneratorFunction<Context>(
        IRpcServerPort<Context> port);

    public interface IServerModuleDefinition<Context>
    {
        Dictionary<string, CallableProcedureServer<Context>> definition { get; }
    }

    public interface IServerModuleProcedure<Context>
    {
        string procedureName { get; }
        uint procedureId { get; }
        CallableProcedureServer<Context> callable { get; }
    }

    public interface IServerModuleDeclaration<Context>
    {
        List<IServerModuleProcedure<Context>> procedures { get; }
    }

    public interface IRpcServerPort<Context>
    {
        event Action OnClose;
        uint portId { get; }
        string portName { get; }
        void RegisterModule(string moduleName, ModuleGeneratorFunction<Context> moduleDefinition);
        Task<IServerModuleDeclaration<Context>> LoadModule(string moduleName);
        AsyncProcedureResultServer CallProcedure(int procedureId, byte[] payload, Context context);
        void Close();
    }
}