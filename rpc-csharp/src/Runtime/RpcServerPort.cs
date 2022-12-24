using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Google.Protobuf;

namespace rpc_csharp
{
    public class RpcServerPort<TContext>
    {
        private readonly Dictionary<string, UniTask<ServerModuleDeclaration>> loadedModules =
            new Dictionary<string, UniTask<ServerModuleDeclaration>>();

        private readonly Dictionary<uint, UnaryCallback<TContext>> procedures =
            new Dictionary<uint, UnaryCallback<TContext>>();

        private readonly Dictionary<uint, ServerStreamCallback<TContext>> serverStreamProcedures =
            new Dictionary<uint, ServerStreamCallback<TContext>>();
        
        private readonly Dictionary<uint, ClientStreamCallback<TContext>> clientStreamProcedures =
            new Dictionary<uint, ClientStreamCallback<TContext>>();
        
        private readonly Dictionary<uint, BidirectionalStreamCallback<TContext>> bidirectionalStreamProcedures =
            new Dictionary<uint, BidirectionalStreamCallback<TContext>>();

        private readonly Dictionary<string, ModuleGeneratorFunction<TContext>> registeredModules =
            new Dictionary<string, ModuleGeneratorFunction<TContext>>();
        
        public readonly Dictionary<uint, CallType> procedureNameToType =
            new Dictionary<uint, CallType>();

        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly CancellationTokenSource portClosedCancellationTokenSource;

        private uint proceduresCount = 0;

        public event Action OnClose;
        public uint portId { get; }
        public string portName { get; }

        private bool disposed = false;

        public RpcServerPort(uint portId, string portName, CancellationToken ct)
        {
            this.portId = portId;
            this.portName = portName;
            portClosedCancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(ct, portClosedCancellationTokenSource.Token);
        }

        public void Close()
        {
            if (disposed)
                return;
            disposed = true;

            loadedModules.Clear();
            procedures.Clear();
            serverStreamProcedures.Clear();
            clientStreamProcedures.Clear();
            bidirectionalStreamProcedures.Clear();
            registeredModules.Clear();
            portClosedCancellationTokenSource.Cancel();
            portClosedCancellationTokenSource.Dispose();
            cancellationTokenSource.Dispose();
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

        public UniTask<ServerModuleDeclaration> LoadModule(string moduleName)
        {
            if (loadedModules.TryGetValue(moduleName, out UniTask<ServerModuleDeclaration> loadedModule))
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

        public async UniTask<(bool called, ByteString result)> TryCallUnaryProcedure(uint procedureId,
            ByteString payload, TContext context)
        {
            if (!procedures.TryGetValue(procedureId, out UnaryCallback<TContext> unaryCallback))
                return (called: false, result: null);

            var result = await unaryCallback(payload, context, cancellationTokenSource.Token);
            return (called: true, result);
        }

        public CallType GetProcedureType(uint procedureId)
        {
            return procedureNameToType.TryGetValue(procedureId, out CallType type) ? type : CallType.Unknown;
        }
        public bool TryCallServerStreamProcedure(uint procedureId, ByteString payload, TContext context,
            out IUniTaskAsyncEnumerable<ByteString> result)
        {
            if (serverStreamProcedures.TryGetValue(procedureId, out ServerStreamCallback<TContext> streamProcedure))
            {
                result = streamProcedure(payload, context);
                return true;
            }

            result = default;
            return false;
        }
        
        public async UniTask<ByteString> TryCallClientStreamProcedure(uint procedureId, IUniTaskAsyncEnumerable<ByteString> payload, TContext context)
        {
            return clientStreamProcedures.TryGetValue(procedureId, out ClientStreamCallback<TContext> clientStreamProcedure) ? await clientStreamProcedure(payload, context, cancellationTokenSource.Token) : null;
        }
        
        public bool TryCallBidiStreamProcedure(uint procedureId, IUniTaskAsyncEnumerable<ByteString> payload, TContext context,
            out IUniTaskAsyncEnumerable<ByteString> result)
        {
            if (bidirectionalStreamProcedures.TryGetValue(procedureId, out BidirectionalStreamCallback<TContext> bidiStreamProcedure))
            {
                result = bidiStreamProcedure(payload, context);
                return true;
            }

            result = default;
            return false;
        }

        private async UniTask<ServerModuleDeclaration> LoadModuleFromGenerator(
            UniTask<ServerModuleDefinition<TContext>> moduleFuture)
        {
            var module = await moduleFuture;

            var moduleUnaryDefinitions = module.definition;
            var moduleServerStreamDefinitions = module.serverStreamDefinition;
            var moduleClientStreamDefinitions = module.clientStreamDefinition;
            var moduleBidiStreamDefinitions = module.bidirectionalStreamDefinition;
            var count = moduleUnaryDefinitions.Count + moduleServerStreamDefinitions.Count +
                                  moduleClientStreamDefinitions.Count + moduleBidiStreamDefinitions.Count;  

            var ret = new ServerModuleDeclaration()
            {
                procedures =
                    new List<ServerModuleProcedureInfo>(count)
            };

            using (var iterator = moduleUnaryDefinitions.GetEnumerator())
            {
                LoadProcedures(iterator, procedures, ret.procedures, CallType.Unary);
            }

            using (var iterator = moduleServerStreamDefinitions.GetEnumerator())
            {
                LoadProcedures(iterator, serverStreamProcedures, ret.procedures, CallType.ServerStream);
            }
            
            using (var iterator = moduleClientStreamDefinitions.GetEnumerator())
            {
                LoadProcedures(iterator, clientStreamProcedures, ret.procedures, CallType.ClientStream);
            }
            
            using (var iterator = moduleBidiStreamDefinitions.GetEnumerator())
            {
                LoadProcedures(iterator, bidirectionalStreamProcedures, ret.procedures, CallType.BidirectionalStream);
            }

            return ret;
        }

        private void LoadProcedures<T>(IEnumerator<KeyValuePair<string, T>> procedureDefinition,
            IDictionary<uint, T> procedureMap, ICollection<ServerModuleProcedureInfo> infoList, CallType type)
        {
            while (procedureDefinition.MoveNext())
            {
                var procedureId = proceduresCount++;
                var procedureName = procedureDefinition.Current.Key;
                procedureMap.Add(procedureId, procedureDefinition.Current.Value);

                infoList.Add(new ServerModuleProcedureInfo(procedureId, procedureName));
                procedureNameToType.Add(procedureId, type);
            }
        }
    }
}