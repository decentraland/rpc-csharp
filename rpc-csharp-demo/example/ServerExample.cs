using System;
using Cysharp.Threading.Tasks;
using Proto;
using rpc_csharp;
using WebSocketSharp.Server;

namespace rpc_csharp_demo.example
{
    /*class TestBinder<Context> : ServiceBinderBase
    {
        public Dictionary<string, UnaryCallback<Context>> unaryMethods;
        public ModuleGeneratorFunction<Context> GetModuleDefinition()
        {
            return port =>
            {
                var definition = new Dictionary<string, UnaryCallback<Context>>();
                definition.Add(
                {
                    
                });
                return Task.FromResult(new ServerModuleDefinition<Context>
                {
                    definition =
                    {
                        
                    }
                });
            };
        }

        public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, UnaryServerMethod<TRequest, TResponse> handler)
        {
            unaryMethods.Add(method.FullName, (payload, context) =>
            {
                return handler.Invoke();
            });
            base.AddMethod(method, handler);
        }

        public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, ClientStreamingServerMethod<TRequest, TResponse> handler)
        {
            base.AddMethod(method, handler);
        }

        public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, ServerStreamingServerMethod<TRequest, TResponse> handler)
        {
            base.AddMethod(method, handler);
        }
    }*/
    public static class ServerExample
    {
        public static void Run()
        {
            var context = new BookContext()
            {
                books = new[]
                {
                    new Book() {Author = "mr menduz", Isbn = 1234, Title = "1001 reasons to write your own OS"},
                    new Book() {Author = "mr cazala", Isbn = 1111, Title = "Advanced CSS"},
                    new Book() {Author = "mr mannakia", Isbn = 7666, Title = "Advanced binary packing"},
                    new Book() {Author = "mr kuruk", Isbn = 7668, Title = "Advanced bots AI"},
                    /*new Book() { Title = "Pato", Author = "QuiereAsado", Isbn = 1234 },
                    new Book() { Title = "Title2", Author = "Owen", Isbn = 5678 },
                    new Book() { Title = "Title3", Author = "Bardock", Isbn = 5678 },
                    new Book() { Title = "Rpc onion layers", Author = "menduz", Isbn = 19997 }*/
                }
            };

            Console.Write("> Creating server");
            var url = $"ws://127.0.0.1:{8080}/";
            var wss = new WebSocketServer(url);

            var rpcServer = new RpcServer<BookContext>();

            rpcServer.SetHandler((port, transport, context) =>
            {
                BookServiceImpl service = new BookServiceImpl();
                port.RegisterModule(service.ServiceName,
                    (port) => { return UniTask.FromResult(service.GetModuleDefinition()); });
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