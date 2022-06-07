using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace rpc_csharp.server
{
    public class RpcServerPort<Context> : IRpcServerPort<Context>
    {
        /*private class ServerModuleDeclaration<Context> : IServerModuleDeclaration<Context>
        {
            public List<server.ServerModuleProcedure<Context>> procedures { set; get; }
        }

        private class ServerModuleProcedure<Context> : server.ServerModuleProcedure<Context>
        {
            public string procedureName { set; get; }
            public uint procedureId { set; get; }
            public CallableProcedureServer<Context> callable { set; get; }
        }*/

        private readonly Dictionary<string, Task<IServerModuleDeclaration<Context>>> loadedModules = new();
        private readonly Dictionary<int, UnaryCallback<Context>> procedures = new();
        private readonly Dictionary<string, ModuleGeneratorFunction<Context>> registeredModules = new();

        private event Action OnClose;
        private uint portId;
        private string portName;

        public static IRpcServerPort<Context> CreateServerPort(uint portId, string portName)
        {
            RpcServerPort<Context> port = new()
            {
                portId = portId,
                portName = portName
            };

            return port;
        }

        private async Task<ServerModuleDeclaration<Context>> LoadModuleFromGenerator(
            Task<ServerModuleDefinition<Context>> moduleFuture)
        {
            var module = await moduleFuture;
            var ret = new ServerModuleDeclaration<Context>()
            {
                procedures = new List<ServerModuleProcedure<Context>>()
            };

            using (var iterator = module.definition.GetEnumerator())
            {
                while (iterator.MoveNext())
                {
                    var procedureId = procedures.Count + 1;
                    var procedureName = iterator.Current.Key;
                    var callable = iterator.Current.Value;
                    procedures.Add(procedureId, iterator.Current.Value);
                    ret.procedures.Add(new ServerModuleProcedure<Context>()
                    {
                        procedureName = procedureName,
                        callable = callable,
                        procedureId = (uint)procedureId,
                    });
                }
            }

            return ret;
        }

        event Action IRpcServerPort<Context>.OnClose
        {
            add => OnClose += value;
            remove => OnClose -= value;
        }

        uint IRpcServerPort<Context>.portId => portId;

        string IRpcServerPort<Context>.portName => portName;

        void IRpcServerPort<Context>.RegisterModule(string moduleName,
            ModuleGeneratorFunction<Context> moduleDefinition)
        {
            if (registeredModules.ContainsKey(moduleName))
            {
                throw new Exception($"module ${moduleName} is already registered for port {portName} ({portId}))");
            }

            registeredModules.Add(moduleName, moduleDefinition);
        }

        Task<ServerModuleDeclaration<Context>> IRpcServerPort<Context>.LoadModule(string moduleName)
        {
            if (loadedModules.TryGetValue(moduleName, out Task<ServerModuleDeclaration<Context>> loadedModule))
            {
                return loadedModule;
            }

            if (!registeredModules.TryGetValue(moduleName, out ModuleGeneratorFunction<Context> moduleGenerator))
            {
                throw new Exception($"Module ${moduleName} is not available for port {portName} ({portId}))");
            }

            var moduleFuture = LoadModuleFromGenerator(moduleGenerator(this));
            loadedModules.Add(moduleName, moduleFuture);

            return moduleFuture;
        }

        public AsyncGenerator<Context> CallStreamProcedure(int procedureId, byte[] payload, Context context)
        {
            throw new NotImplementedException();
        }

        public UnaryCallback<Context> CallUnaryProcedure(int procedureId, byte[] payload, Context context)
        {
            if (!procedures.TryGetValue(procedureId, out UnaryCallback<Context> procedure))
            {
                throw new Exception($"procedureId ${procedureId} is missing in {portName} ({portId}))");
            }

            return () => procedure(payload, context);
        }

        void IRpcServerPort<Context>.Close()
        {
            loadedModules.Clear();
            procedures.Clear();
            registeredModules.Clear();
            OnClose?.Invoke();
        }
    }
}