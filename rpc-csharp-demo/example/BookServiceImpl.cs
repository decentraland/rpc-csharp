using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Proto;
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
                    return new Book()
                    {
                        Author = "menduz",
                        Isbn = request.Isbn,
                        Title = "Rpc onion layers",
                    };
                },
                (request, context) =>
                {
                    return QueryBooks(request, context);
                });

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
        }
    }
}