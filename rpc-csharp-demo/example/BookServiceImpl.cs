using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace rpc_csharp_demo.example
{
    public class BookContext
    {
        public Book[] books;
    }

    public class BookServiceImpl : BookServiceGen<BookContext>
    {
        public override Task<Book> GetBook(GetBookRequest request, BookContext context)
        {
            return Task.FromResult(new Book()
            {
                Author = "menduz",
                Isbn = request.Isbn,
                Title = "Rpc onion layers",
            });
            /*foreach (var book in context.books)
            {
                if (book.Isbn == request.Isbn)
                {
                    return Task.FromResult(book);
                }
            }
            return Task.FromResult(new Book()); // TODO: Implement error pipeline*/
        }

        public override IEnumerator<Task<Book>> QueryBooks(GetBookRequest request, BookContext context)
        {
            return context.books.Select(Task.FromResult).GetEnumerator();
        }
    }
}