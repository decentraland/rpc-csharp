using Cysharp.Threading.Tasks;
using rpc_csharp;

namespace rpc_csharp_test.codegen
{
    public class ClientApiService
    {
        private readonly RpcClientModule module;

        public ClientApiService(RpcClientModule module)
        {
            this.module = module;
        }
        public UniTask<Book> GetBook(GetBookRequest request)
        {
            return module.CallUnaryProcedure<Book>("GetBook", request);
        }

        public IUniTaskAsyncEnumerable<Book> QueryBooks(QueryBooksRequest request)
        {
            return module.CallServerStream<Book>("GetBook", request);
        }

        public UniTask<Book> GetBookStream(IUniTaskAsyncEnumerable<GetBookRequest> streamRequest)
        {
            return module.CallClientStream<Book>("GetBookStream", streamRequest);
        }

        public IUniTaskAsyncEnumerable<Book> QueryBooksStream(IUniTaskAsyncEnumerable<GetBookRequest> streamRequest)
        {
            return module.CallBidirectionalStream<Book>("QueryBooksStream", streamRequest);
        }
    }
}