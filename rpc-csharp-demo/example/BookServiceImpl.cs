using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace rpc_csharp_demo.example
{
    public class BookContext
    {
        public Book[] books;
    }

    public class BookServiceImpl : BookServiceGen<BookContext>
    {
        public override UniTask<Book> GetBook(GetBookRequest request, BookContext context)
        {
            return UniTask.FromResult(new Book()
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

        public override IEnumerator<UniTask<Book>> QueryBooks(GetBookRequest request, BookContext context)
        {
            return context.books.Select(UniTask.FromResult).GetEnumerator();
        }
    }
}