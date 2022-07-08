using System.Collections.Generic;
using System.Linq;
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
                    return QueryBooks(context);
                });

            IEnumerator<Book> QueryBooks(BookContext context)
            {
                using (var iterator = context.books.AsEnumerable()!.GetEnumerator())
                {
                    while (iterator.MoveNext())
                    {
                        yield return new Book(iterator.Current);
                    }
                }
            }
        }
    }
}