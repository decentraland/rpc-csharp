using System.Linq;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using rpc_csharp;

namespace rpc_csharp_demo.example
{
    public class BookContext
    {
        public Book[] books;
    }

    public static class BookServiceImpl
    {
        public static void RegisterService(RpcServerPort<BookContext> port)
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
                async (streamRequest, context, ct) =>
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
                (streamRequest, context) =>
                {
                    return UniTaskAsyncEnumerable.Create<Book>(async (writer, token) =>
                    {
                        await foreach (var request in streamRequest)
                        {
                            if (token.IsCancellationRequested) break;
                            token.ThrowIfCancellationRequested();

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

            IUniTaskAsyncEnumerable<Book> QueryBooks(QueryBooksRequest request, BookContext context)
            {
                return UniTaskAsyncEnumerable.Create<Book>(async (writer, token) =>
                {
                    foreach (var book in context.books)
                    {
                        if (token.IsCancellationRequested) break;
                        await writer.YieldAsync(book); // instead of `yield return`
                    }
                });
            }
        }
    }
}