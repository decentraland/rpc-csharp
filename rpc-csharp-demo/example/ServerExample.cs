using rpc_csharp;
using WebSocketSharp.Server;

namespace rpc_csharp_demo.example
{
    public static class ServerExample
    {
        public static void Run()
        {
            Console.Write("> Creating server");
            var url = $"ws://localhost:{8080}/";
            WebSocketServer wss = new WebSocketServer(url);

            RpcServer rpcServer = new RpcServer();

            wss.AddWebSocketService("/", () =>
            {
                var transport = new WebSocketServerTransport();
                Console.Write("> Create transport");
                rpcServer.AttachTransport(transport);
                return transport;
            });

            wss.Start();

/*

  console.log("> Creating server")
  const rpcServer = createRpcServer<TestContext>({})
  // the handler function will be called every time a port is created.
  // it should register the available APIs/Modules for the specified port
  rpcServer.setHandler(async function handler(port) {
    console.log("  Creating server port: " + port.portName)
    registerBookServiceServerImplementation(port)
  })

  console.log("> Creating client and server MemoryTransport")
  const wss = new WebSocketServer({ port: 8080 })
  wss.on('connection', function connection(ws: any, req: any) {
    const serverSocket = WebSocketTransport(ws)

    // connect the "socket" to the server
    console.log("> Attaching transport")
    rpcServer.attachTransport(serverSocket, context)

    wss.close()
  })

*/
        }
    }
}
