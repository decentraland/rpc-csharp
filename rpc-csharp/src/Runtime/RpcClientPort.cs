using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace rpc_csharp
{
    public class RpcClientPort
    {
        internal readonly string portName;
        internal readonly uint portId;
        internal readonly ClientRequestDispatcher dispatcher;
        public Action<string> OnPortClosed;

        internal static async UniTask<RpcClientPort> CreatePort(ClientRequestDispatcher dispatcher, string portName)
        {
            var createPortResponse = await dispatcher.SendAndWaitForResponse<CreatePortResponse>(new CreatePort
            {
                PortName = portName
            }, RpcMessageTypes.CreatePort);
            
            return new RpcClientPort(dispatcher, portName, createPortResponse.PortId);
        }

        private RpcClientPort(ClientRequestDispatcher dispatcher, string portName, uint portId)
        {
            this.dispatcher = dispatcher;
            this.portName = portName;
            this.portId = portId;
        }

        public async UniTask<RpcClientModule> LoadModule(string serviceName)
        {
            var requestModuleResponse = await dispatcher.SendAndWaitForResponse<RequestModuleResponse>(new RequestModule
            {
                ModuleName = serviceName,
                PortId = portId
            }, RpcMessageTypes.RequestModule);

            var procedures = new Dictionary<string, uint>(requestModuleResponse.Procedures.Count);

            foreach (var moduleProcedure in requestModuleResponse.Procedures)
            {
                procedures.Add(moduleProcedure.ProcedureName, moduleProcedure.ProcedureId);
            }

            return new RpcClientModule(this, procedures);
        }
        
        public void Close()
        {
            dispatcher.SendAndForget(new DestroyPort()
            {
                PortId = portId
            }, RpcMessageTypes.DestroyPort);
            
            // Warning: we don't expect a response, we close the port immediately from the client perspective
            OnPortClosed?.Invoke(portName);
        }
    }
}