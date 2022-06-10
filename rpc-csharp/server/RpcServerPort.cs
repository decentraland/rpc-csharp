using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Google.Protobuf;

namespace rpc_csharp.server
{
    public class RpcServerPort<TContext>
    {
        private readonly Dictionary<string, UniTask<ServerModuleDeclaration<TContext>>> loadedModules =
            new Dictionary<string, UniTask<ServerModuleDeclaration<TContext>>>();

        //UnaryCallback<TContext> | AsyncGenerator<TContext>
        private readonly Dictionary<uint, UnaryCallback<TContext>> procedures =
            new Dictionary<uint, UnaryCallback<TContext>>();

        private readonly Dictionary<uint, AsyncGenerator<TContext>> streamProcedures =
            new Dictionary<uint, AsyncGenerator<TContext>>();

        private readonly Dictionary<string, ModuleGeneratorFunction<TContext>> registeredModules =
            new Dictionary<string, ModuleGeneratorFunction<TContext>>();

        private event Action OnClose;
        public uint portId { get; }
        public string portName { get; }

        public RpcServerPort(uint portId, string portName)
        {
            this.portId = portId;
            this.portName = portName;
        }

        public void Close()
        {
            loadedModules.Clear();
            procedures.Clear();
            streamProcedures.Clear();
            registeredModules.Clear();
            OnClose?.Invoke();
        }

        public void RegisterModule(string moduleName,
            ModuleGeneratorFunction<TContext> moduleDefinition)
        {
            if (registeredModules.ContainsKey(moduleName))
            {
                throw new Exception($"module ${moduleName} is already registered for port {portName} ({portId}))");
            }

            registeredModules.Add(moduleName, moduleDefinition);
        }

        public UniTask<ServerModuleDeclaration<TContext>> LoadModule(string moduleName)
        {
            if (loadedModules.TryGetValue(moduleName, out UniTask<ServerModuleDeclaration<TContext>> loadedModule))
            {
                return loadedModule;
            }

            if (!registeredModules.TryGetValue(moduleName, out ModuleGeneratorFunction<TContext> moduleGenerator))
            {
                throw new Exception($"Module ${moduleName} is not available for port {portName} ({portId}))");
            }

            var moduleFuture = LoadModuleFromGenerator(moduleGenerator(this));
            loadedModules.Add(moduleName, moduleFuture);

            return moduleFuture;
        }

        public async UniTask<object> CallProcedure(uint procedureId, ByteString payload, TContext context)
        {
            if (procedures.TryGetValue(procedureId, out UnaryCallback<TContext> unaryCallback))
            {
                return await unaryCallback(payload, context);
            }

            if (streamProcedures.TryGetValue(procedureId, out AsyncGenerator<TContext> streamProcedure))
            {
                return await streamProcedure(payload, context);
            }

            throw new Exception($"procedureId ${procedureId} is missing in {portName} ({portId}))");
        }

        private async UniTask<ServerModuleDeclaration<TContext>> LoadModuleFromGenerator(
            UniTask<ServerModuleDefinition<TContext>> moduleFuture)
        {
            var module = await moduleFuture;
            var ret = new ServerModuleDeclaration<TContext>()
            {
                procedures = new List<ServerModuleProcedure<TContext>>()
            };

            using (var iterator = module.definition.GetEnumerator())
            {
                while (iterator.MoveNext())
                {
                    var procedureId = (uint) (procedures.Count + 1);
                    var procedureName = iterator.Current.Key;
                    var callable = iterator.Current.Value;
                    procedures.Add(procedureId, iterator.Current.Value);
                    ret.procedures.Add(new ServerModuleProcedure<TContext>()
                    {
                        procedureName = procedureName,
                        callable = callable,
                        procedureId = procedureId,
                    });
                }
            }

            // TODO: Refactor this copy-paste
            using (var iterator = module.streamDefinition.GetEnumerator())
            {
                while (iterator.MoveNext())
                {
                    var procedureId = (uint) (procedures.Count + 1);
                    var procedureName = iterator.Current.Key;
                    var callable = iterator.Current.Value;
                    streamProcedures.Add(procedureId, iterator.Current.Value);
                    ret.procedures.Add(new ServerModuleProcedure<TContext>()
                    {
                        procedureName = procedureName,
                        asyncCallable = callable,
                        procedureId = procedureId,
                    });
                }
            }

            return ret;
        }
    }
}