using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using NUnit.Framework;
using rpc_csharp;
using rpc_csharp_demo.example;
using rpc_csharp.transport;

namespace rpc_csharp_test
{
    public class IntegrationTest
    {
        private ClientBookService clientBookService;
        private BookContext context;

        [SetUp]
        public async UniTask Setup()
        {
            var (client, server) = MemoryTransport.Create();
            TestUtils.InstrumentTransport(client, "message->client");
            TestUtils.InstrumentTransport(server, "message->server");

            context = new BookContext()
            {
                books = new[]
                {
                    new Book() {Author = "mr menduz", Isbn = 1234, Title = "1001 reasons to write your own OS"},
                    new Book() {Author = "mr cazala", Isbn = 1111, Title = "Advanced CSS"},
                    new Book() {Author = "mr mannakia", Isbn = 7666, Title = "Advanced binary packing"},
                    new Book() {Author = "mr kuruk", Isbn = 7668, Title = "Base64 hater"},
                    new Book() {Author = "mr pato", Isbn = 7669, Title = "Base64 fan"},
                }
            };
            var rpcServer = new RpcServer<BookContext>();
            rpcServer.AttachTransport(server, context);
            rpcServer.SetHandler((port, transport, testContext) =>
            {
                BookServiceCodeGen.RegisterService(port, new BookServiceImpl());
            });

            //testClient = await TestClient.Create(client, BookServiceImpl.ServiceName);
            var rpcClient = new RpcClient(client);
            var clientPort = await rpcClient.CreatePort("my-port");
            var clientModule = await clientPort.LoadModule(BookServiceCodeGen.ServiceName);
            clientBookService = new ClientBookService(clientModule);
        }

        IUniTaskAsyncEnumerable<Book> QueryBooks(QueryBooksRequest request, BookContext context)
        {
            return UniTaskAsyncEnumerable.Create<Book>(async (writer, token) =>
            {
                using (var iterator = context.books.AsEnumerable()!.GetEnumerator())
                {
                    while (iterator.MoveNext() && !token.IsCancellationRequested)
                    {
                        await writer.YieldAsync(iterator.Current); // instead of `yield return`
                    }
                }
            });
        }

        [Test]
        public async UniTask ProcedureCall()
        {
            var expectedBook = context.books[4];

            var book = await clientBookService.GetBook(new GetBookRequest()
            {
                Isbn = expectedBook.Isbn
            });

            Assert.AreEqual(expectedBook.Author, book.Author);
            Assert.AreEqual(expectedBook.Isbn, book.Isbn);
            Assert.AreEqual(expectedBook.Title, book.Title);
        }

        [Test]
        public async UniTask ServerStreamCall()
        {
            List<Book> books = new List<Book>();
            var query = new QueryBooksRequest()
            {
                AuthorPrefix = "mr"
            };

            await foreach (var element in clientBookService.QueryBooks(query))
            {
                var book = element;
                books.Add(book);
            }

            Assert.AreEqual(context.books.Length, books.Count);

            for (int i = 0; i < books.Count; i++)
            {
                var expectedBook = context.books[i];
                var book = books[i];
                Assert.AreEqual(expectedBook.Author, book.Author);
                Assert.AreEqual(expectedBook.Isbn, book.Isbn);
                Assert.AreEqual(expectedBook.Title, book.Title);
            }
        }

        [Test]
        public async UniTask ServerStreamCallWithNullElements()
        {
            context.books = new[]
            {
                null,
                null,
                null,
                null,
                new Book() {Author = "mr pato", Isbn = 7669, Title = "Base64 fan"},
                null
            };

            var expectedBook = context.books[4];


            List<Book> books = new List<Book>();
            var query = new QueryBooksRequest()
            {
                AuthorPrefix = "mr"
            };

            await foreach (var element in clientBookService.QueryBooks(query))
            {
                var book = element;
                books.Add(book);
            }

            Assert.AreEqual(1, books.Count);

            var returnedBook = books[0];
            Assert.AreEqual(expectedBook.Author, returnedBook.Author);
            Assert.AreEqual(expectedBook.Isbn, returnedBook.Isbn);
            Assert.AreEqual(expectedBook.Title, returnedBook.Title);
        }
        
        [Test]
        public async UniTask ClientStreamCall()
        {
            var query = UniTaskAsyncEnumerable.Create<GetBookRequest>(async (writer, token) =>
            {
                await writer.YieldAsync(new GetBookRequest() {Isbn = 1234});
                await writer.YieldAsync(new GetBookRequest() {Isbn = 1111});
                await writer.YieldAsync(new GetBookRequest() {Isbn = 7666});
                await writer.YieldAsync(new GetBookRequest() {Isbn = 7668});
                await writer.YieldAsync(new GetBookRequest() {Isbn = 7669});
            });

            var response = clientBookService.GetBookStream(query);
            
            var expectedBook = context.books.Last();
            var book = await response;
            Assert.AreEqual(expectedBook.Author, book.Author);
            Assert.AreEqual(expectedBook.Isbn, book.Isbn);
            Assert.AreEqual(expectedBook.Title, book.Title);
        }
        
        [Test]
        public async UniTask BidirectionalStreamCall()
        {
            var query = UniTaskAsyncEnumerable.Create<GetBookRequest>(async (writer, token) =>
            {
                await writer.YieldAsync(new GetBookRequest() {Isbn = 1234});
                await writer.YieldAsync(new GetBookRequest() {Isbn = 1111});
                await writer.YieldAsync(new GetBookRequest() {Isbn = 7666});
                await writer.YieldAsync(new GetBookRequest() {Isbn = 7668});
                await writer.YieldAsync(new GetBookRequest() {Isbn = 7669});
            });

            List<Book> books = new List<Book>();

            await foreach (var element in clientBookService.QueryBooksStream(query))
            {
                var book = element;
                books.Add(book);
            }

            Assert.AreEqual(context.books.Length, books.Count);

            for (int i = 0; i < books.Count; i++)
            {
                var expectedBook = context.books[i];
                var book = books[i];
                Assert.AreEqual(expectedBook.Author, book.Author);
                Assert.AreEqual(expectedBook.Isbn, book.Isbn);
                Assert.AreEqual(expectedBook.Title, book.Title);
            }
        }
    }
}