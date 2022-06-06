using Google.Protobuf.WellKnownTypes;
using rpc_csharp;
using WebSocketSharp.Server;

namespace rpc_csharp_demo.example
{
    public class LeContext
    {
        public string hello;
    }
    public static class ServerExample
    {
        public static void Run()
        {
            LeContext context = new LeContext()
            {
                hello = "world"
            };
            
            Console.Write("> Creating server");
            var url = $"ws://localhost:{8080}/";
            var wss = new WebSocketServer(url);

            var rpcServer = new RpcServer<LeContext>();
            
            rpcServer.SetHandler((port, transport, leContext) =>
            {
                Console.WriteLine(port.portId);
            });

            wss.AddWebSocketService("/", () =>
            {
                var transport = new WebSocketServerTransport();
                Console.Write("> Create transport");
                rpcServer.AttachTransport(transport, context);
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
