using Grpc.Core;
using rpc_csharp.server;

namespace rpc_csharp_demo.example;
public class BookContext
{
    public Book[] books;
}
public class BookServiceImpl : BookServiceGen<BookContext>
{
    public override Task<Book> GetBook(GetBookRequest request, BookContext context)
    {
        return Task.FromResult(context.books[0]);
    }

    public override IEnumerator<Task<Book>> QueryBooks(GetBookRequest request, BookContext context)
    {
        yield return Task.FromResult(context.books[0]);
        
        yield return Task.FromResult(context.books[1]);
    }
}