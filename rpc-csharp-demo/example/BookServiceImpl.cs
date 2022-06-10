using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Proto;

namespace rpc_csharp_demo.example
{
    public class BookContext
    {
        public Book[] books;
    }

    public class BookServiceImpl : BookService<BookContext>
    {
        public override async UniTask<Book> GetBook(GetBookRequest request, BookContext context)
        {
            return new Book()
            {
                Author = "menduz",
                Isbn = request.Isbn,
                Title = "Rpc onion layers",
            };
            /*foreach (var book in context.books)
            {
                if (book.Isbn == request.Isbn)
                {
                    return Task.FromResult(book);
                }
            }
            return Task.FromResult(new Book()); // TODO: Implement error pipeline*/
        }

        public override async UniTask<IEnumerator<Book>> QueryBooks(GetBookRequest request,
            BookContext context)
        {
            return context.books.AsEnumerable().GetEnumerator();
        }
    }
}