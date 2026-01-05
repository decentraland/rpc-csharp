using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using rpc_csharp.transport;

namespace rpc_csharp
{
    public class RpcClient : IDisposable
    {
        public ClientRequestDispatcher dispatcher { private set; get; }

        private Dictionary<string, RpcClientPort> portByName = new Dictionary<string, RpcClientPort>();

        private readonly ITransport transport;

        public RpcClient(ITransport transport)
        {
            this.transport = transport;
            dispatcher = new ClientRequestDispatcher(transport);
        }

        public async UniTask<RpcClientPort> CreatePort(string portName)
        {
            RpcClientPort rpcClientPort;
            
            if (portByName.TryGetValue(portName, out rpcClientPort))
            {
                return rpcClientPort;
            }

            rpcClientPort = await RpcClientPort.CreatePort(dispatcher, portName);
            portByName.Add(portName, rpcClientPort);

            rpcClientPort.OnPortClosed += OnPortClosed;
            
            return rpcClientPort;
        }

        private void OnPortClosed(string portName)
        {
            portByName.Remove(portName);
        }

        public void Dispose()
        {
            // the ports should be removed in the server when it disconnect
            // no need to send the message to the server
            portByName.Clear();
            
            dispatcher.Dispose();
        }
    }
}