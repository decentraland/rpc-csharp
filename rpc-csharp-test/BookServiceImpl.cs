using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;

namespace rpc_csharp_demo.example
{
    public class BookContext
    {
        public Book[] books;
    }

    public class BookServiceImpl : IBookService<BookContext>
    {
        protected override async UniTask<Book> GetBook(GetBookRequest request, BookContext context,
            CancellationToken ct)
        {
            foreach (var book in context.books)
            {
                if (request.Isbn == book.Isbn)
                {
                    return book;
                }
            }

            return new Book();
        }

        protected override IUniTaskAsyncEnumerable<Book> QueryBooks(QueryBooksRequest request, BookContext context)
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

        protected override async UniTask<Book> GetBookStream(IUniTaskAsyncEnumerable<GetBookRequest> streamRequest, BookContext context, CancellationToken ct)
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
        }

        protected override IUniTaskAsyncEnumerable<Book> QueryBooksStream(IUniTaskAsyncEnumerable<GetBookRequest> streamRequest, BookContext context)
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
        }
    }
}