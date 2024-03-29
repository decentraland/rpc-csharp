
// AUTOGENERATED, DO NOT EDIT
// Type definitions for server implementations of ports.
// package: 
// file: api.proto
using Cysharp.Threading.Tasks;
using rpc_csharp;

public class ClientBookService
{
  private readonly RpcClientModule module;

  public ClientBookService(RpcClientModule module)
  {
      this.module = module;
  }

  public UniTask<Book> GetBook(GetBookRequest request)
  {
      return module.CallUnaryProcedure<Book>("GetBook", request);
  }

  public IUniTaskAsyncEnumerable<Book> QueryBooks(QueryBooksRequest request)
  {
      return module.CallServerStream<Book>("QueryBooks", request);
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
