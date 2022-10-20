using System;
using rpc_csharp;
using WebSocketSharp.Server;

namespace rpc_csharp_demo.example
{
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
                    new Book() {Author = "mr pato", Isbn = 777, Title = "Buy him a thermo"},
                }
            };

            Console.Write("> Creating server");
            var url = $"ws://127.0.0.1:{8080}/";
            var wss = new WebSocketServer(url);

            var rpcServer = new RpcServer<BookContext>();

            rpcServer.SetHandler((port, transport, context) =>
            {
                BookServiceImpl.RegisterService(port, new BookServiceImpl());
            });

            wss.AddWebSocketService("/", () =>
            {
                var transport = new WebSocketServerTransport();
                Console.Write("> Create transport");
                rpcServer.AttachTransport(transport, context);
                return transport;
            });

            wss.Start();
            
            while(true) {}
        }
    }
}