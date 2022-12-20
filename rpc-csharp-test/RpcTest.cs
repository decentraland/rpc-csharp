using System;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using rpc_csharp;
using rpc_csharp.transport;
using rpc_csharp_demo.example;

namespace rpc_csharp_test
{
    public class RpcTest
    {
        private readonly Book[] booksMock = {
            new Book() {Author = "mr menduz", Isbn = 1234, Title = "1001 reasons to write your own OS"},
            new Book() {Author = "mr cazala", Isbn = 1111, Title = "Advanced CSS"},
            new Book() {Author = "mr mannakia", Isbn = 7666, Title = "Advanced binary packing"},
            new Book() {Author = "mr kuruk", Isbn = 7668, Title = "Base64 hater"},
            new Book() {Author = "mr pato", Isbn = 7669, Title = "Base64 fan"},
        };
            
        [Test]
        public async UniTask ShouldHandleDoubleDispose()
        {
            ClientBookService clientBookService;
            BookContext context;
            var (client, server) = MemoryTransport.Create();

            context = new BookContext()
            {
                books = booksMock
            };
            var rpcServer = new RpcServer<BookContext>();
            rpcServer.AttachTransport(server, context);
            rpcServer.SetHandler((port, transport, testContext) =>
            {
                BookServiceCodeGen.RegisterService(port, new BookServiceImpl());
            });
            
            var rpcClient = new RpcClient(client);
            var clientPort = await rpcClient.CreatePort("my-port");
            var clientModule = await clientPort.LoadModule(BookServiceCodeGen.ServiceName);
            clientBookService = new ClientBookService(clientModule);
            await clientBookService.GetBook(new GetBookRequest()
            {
                Isbn = 7666
            });
            
            rpcServer.Dispose();
            rpcServer.Dispose();
            rpcClient.Dispose();
            rpcClient.Dispose();
        }
        
        [Test]
        public async UniTask ShouldCreatePortAndDestroyItAfterCloseRpc()
        {
            ClientBookService clientBookService;
            BookContext context;
            var (client, server) = MemoryTransport.Create();

            context = new BookContext()
            {
                books = booksMock
            };
            var rpcServer = new RpcServer<BookContext>();
            rpcServer.AttachTransport(server, context);
            rpcServer.SetHandler((port, transport, testContext) =>
            {
                BookServiceCodeGen.RegisterService(port, new BookServiceImpl());
            });
            
            var rpcClient = new RpcClient(client);
            var clientPort = await rpcClient.CreatePort("my-port");
            var clientModule = await clientPort.LoadModule(BookServiceCodeGen.ServiceName);
            clientBookService = new ClientBookService(clientModule);
            await clientBookService.GetBook(new GetBookRequest()
            {
                Isbn = 7666
            });
            
            rpcServer.Dispose();
            rpcClient.Dispose();
            
            Assert.AreEqual(rpcServer.ports.Count, 0);
        }
        
        [Test]
        public async UniTask ShouldCreateAndDestroyPort()
        {
            ClientBookService clientBookService;
            BookContext context;
            var (client, server) = MemoryTransport.Create();

            context = new BookContext()
            {
                books = booksMock
            };
            var rpcServer = new RpcServer<BookContext>();
            rpcServer.AttachTransport(server, context);
            rpcServer.SetHandler((port, transport, testContext) =>
            {
                BookServiceCodeGen.RegisterService(port, new BookServiceImpl());
            });
            
            var rpcClient = new RpcClient(client);
            var clientPort = await rpcClient.CreatePort("my-port");
            var clientModule = await clientPort.LoadModule(BookServiceCodeGen.ServiceName);
            clientBookService = new ClientBookService(clientModule);
            await clientBookService.GetBook(new GetBookRequest()
            {
                Isbn = 7666
            });
            
            clientPort.Close();

            Assert.AreEqual(rpcServer.ports.Count, 0);
            
            rpcServer.Dispose();
            rpcClient.Dispose();
        }
    }
}