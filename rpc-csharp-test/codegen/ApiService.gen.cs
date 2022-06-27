// AUTOGENERATED, DO NOT EDIT
// Type definitions for server implementations of ports.
// package: Proto
// file: api.proto
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Google.Protobuf;
using rpc_csharp.protocol;
using rpc_csharp;
namespace Proto {
public abstract class BookService<Context>
{
  public const string ServiceName = "BookService";

  public delegate UniTask<Book> GetBook(GetBookRequest request, Context context);

  public delegate IEnumerator<Book> QueryBooks(QueryBooksRequest request, Context context);

  public static void RegisterService(RpcServerPort<Context> port, GetBook getBook, QueryBooks queryBooks)
  {
    var result = new ServerModuleDefinition<Context>();
      
    result.definition.Add("GetBook", async (payload, context) => { var res = await getBook(GetBookRequest.Parser.ParseFrom(payload), context); return res?.ToByteString(); });
    result.streamDefinition.Add("QueryBooks", (payload, context) => { return new ProtocolHelpers.StreamEnumerator<Book>(queryBooks(QueryBooksRequest.Parser.ParseFrom(payload), context)); });

    port.RegisterModule(ServiceName, (port) => UniTask.FromResult(result));
  }
}
}
