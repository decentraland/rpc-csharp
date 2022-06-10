using System.Linq;
using Proto;
using rpc_csharp.server;

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
                (request, context) =>
                {
                    return new Book()
                    {
                        Author = "menduz",
                        Isbn = request.Isbn,
                        Title = "Rpc onion layers",
                    };
                },
                (request, context) => { return context.books.AsEnumerable()!.GetEnumerator(); });
        }
    }
}