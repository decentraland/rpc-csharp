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
        foreach (var book in context.books)
        {
            if (book.Isbn == request.Isbn)
            {
                return Task.FromResult(book);
            }
        }
        return Task.FromResult(new Book()); // TODO: Implement error pipeline
    }

    public override IEnumerator<Task<Book>> QueryBooks(GetBookRequest request, BookContext context)
    {
        return context.books.Select(Task.FromResult).GetEnumerator();
    }
}