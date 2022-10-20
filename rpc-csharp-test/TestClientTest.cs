using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using NUnit.Framework;
using rpc_csharp;
using rpc_csharp.transport;

namespace rpc_csharp_test
{
    public class TestClientTest
    {
        private TestClient testClient;
        private BookContext context;

        private class BookContext
        {
            public Book[] books;
        }

        [SetUp]
        public async UniTask Setup()
        {
            var (client, server) = MemoryTransport.Create();

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
                BookService<BookContext>.RegisterService(port,
                    async (request, context, ct) =>
                    {
                        foreach (var book in context.books)
                        {
                            if (request.Isbn == book.Isbn)
                            {
                                return book;
                            }
                        }

                        return new Book();
                    },
                    (request, context) => QueryBooks(request, context),
                    async (streamRequest, bookContext, ct) =>
                    {
                        var selectedBook = new Book();
                        await foreach (var request in streamRequest)
                        {
                            if (ct.IsCancellationRequested) break;

                            foreach (var book in context.books)
                            {
                                if (request.Isbn == book.Isbn)
                                {
                                    selectedBook = book;
                                }
                            }
                        }

                        return selectedBook;
                    },
                    (streamRequest, bookContext) =>
                    {
                        return UniTaskAsyncEnumerable.Create<Book>(async (writer, token) =>
                        {
                            await foreach (var request in streamRequest)
                            {
                                if (token.IsCancellationRequested) break;

                                foreach (var book in context.books)
                                {
                                    if (request.Isbn == book.Isbn)
                                    {
                                        await writer.YieldAsync(book); // instead of `yield return`
                                    }
                                }
                            }
                        });
                    });
            });

            testClient = await TestClient.Create(client, BookService<BookContext>.ServiceName);
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

            var book = await testClient.CallProcedure<Book>("GetBook", new GetBookRequest()
            {
                Isbn = expectedBook.Isbn
            });

            Assert.AreEqual(expectedBook.Author, book.Author);
            Assert.AreEqual(expectedBook.Isbn, book.Isbn);
            Assert.AreEqual(expectedBook.Title, book.Title);
        }

        [Test]
        public async UniTask StreamCall()
        {
            List<Book> books = new List<Book>();
            var query = new QueryBooksRequest()
            {
                AuthorPrefix = "mr"
            };

            await foreach (var futureElement in testClient.CallStream<Book>("QueryBooks", query))
            {
                var book = await futureElement;
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
        public async UniTask StreamCallWithNullElements()
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

            await foreach (var futureElement in testClient.CallStream<Book>("QueryBooks", query))
            {
                var book = await futureElement;
                books.Add(book);
            }

            Assert.AreEqual(1, books.Count);

            var returnedBook = books[0];
            Assert.AreEqual(expectedBook.Author, returnedBook.Author);
            Assert.AreEqual(expectedBook.Isbn, returnedBook.Isbn);
            Assert.AreEqual(expectedBook.Title, returnedBook.Title);
        }
    }
}