using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Google.Protobuf;
using rpc_csharp.server;

namespace rpc_csharp_demo.example
{
    public abstract class BookServiceGen<Context>
    {
        public string ServiceName = "BookService";

        // Unary
        public abstract UniTask<Book> GetBook(GetBookRequest request, Context context);

        // Stream
        public abstract IEnumerator<UniTask<Book>> QueryBooks(GetBookRequest request, Context context);

        // Generated code
        public ServerModuleDefinition<Context> GetModuleDefinition()
        {
            var result = new ServerModuleDefinition<Context>();

            result.definition.Add("GetBook", async (payload, context) =>
            {
                var book = await GetBook(GetBookRequest.Parser.ParseFrom(payload), context);
                return book.ToByteArray();
            });

            result.streamDefinition.Add("QueryBooks", (payload, context) =>
            {
                var generator = QueryBooks(GetBookRequest.Parser.ParseFrom(payload), context);
                return RegisterStreamFn(generator);
            });

            return result;
        }

        // Fixed code
        private IEnumerator<UniTask<byte[]>> RegisterStreamFn<T>(IEnumerator<UniTask<T>> generator)
            where T : IMessage
        {
            using (var iterator = generator)
            {
                while (iterator.MoveNext())
                {
                    var response = iterator.Current.GetAwaiter().GetResult().ToByteArray();
                    yield return UniTask.FromResult(response);
                }
            }
        }
    }
}