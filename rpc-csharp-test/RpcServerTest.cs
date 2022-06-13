using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf;
using NUnit.Framework;
using Proto;
using rpc_csharp;
using rpc_csharp.protocol;
using rpc_csharp.transport;

namespace rpc_csharp_test
{
    public class RpcServerTest
    {
        private ITransport client;
        private ITransport server;
        private BookContext context;
        
        internal class BookContext
        {
            public Book[] books;
        }

        [SetUp]
        public void Setup()
        {
            (client, server) = MemoryTransport.Create();
            
            context = new BookContext()
            {
                books = new[]
                {
                    new Book() {Author = "mr menduz", Isbn = 1234, Title = "1001 reasons to write your own OS"},
                    new Book() {Author = "mr cazala", Isbn = 1111, Title = "Advanced CSS"},
                    new Book() {Author = "mr mannakia", Isbn = 7666, Title = "Advanced binary packing"},
                    new Book() {Author = "mr kuruk", Isbn = 7668, Title = "Base64 hater"},
                    new Book() {Author = "mr pato", Isbn = 7669, Title = "Base64 fan"},
                }
            };
            var rpcServer = new RpcServer<BookContext>();
            rpcServer.AttachTransport(server, context);
            rpcServer.SetHandler((port, transport, testContext) =>
            {
                BookService<BookContext>.RegisterService(port,
                    (request, context) =>
                    {
                        foreach (var book in context.books)
                        {
                            if (request.Isbn == book.Isbn)
                            {
                                return book;
                            }
                        }

                        return new Book();
                    },
                    (request, context) => { return context.books.AsEnumerable()!.GetEnumerator(); });
            });
        }

        private void CreatePort(TransportAsyncWrapper clientWrapper)
        {
            var request = new CreatePort
            {
                MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                    RpcMessageTypes.CreatePort,
                    0
                ),
                PortName = "my-port"
            };
            client.SendMessage(request.ToByteArray());

            var response = clientWrapper.GetNextMessage().GetAwaiter().GetResult();
            var expectedResponse = new CreatePortResponse
            {
                MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                    RpcMessageTypes.CreatePortResponse,
                    0
                ),
                PortId = 1
            };

            var parsedResponse = CreatePortResponse.Parser.ParseFrom(response);
            Assert.True(expectedResponse.Equals(parsedResponse));
            
            Assert.True(clientWrapper.GetMessagesCount() == 0);
        }

        public void LoadModule(TransportAsyncWrapper clientWrapper)
        {
            var request = new RequestModule()
            {
                MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                    RpcMessageTypes.RequestModule,
                    1
                ),
                ModuleName = "BookService",
                PortId = 1
            };
            client.SendMessage(request.ToByteArray());

            var response = clientWrapper.GetNextMessage().GetAwaiter().GetResult();
            var expectedResponse = new RequestModuleResponse
            {
                MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                    RpcMessageTypes.RequestModuleResponse,
                    1
                ),
                PortId = 1,
                Procedures =
                {
                    new ModuleProcedure()
                    {
                        ProcedureId = 0,
                        ProcedureName = "GetBook",
                    },
                    new ModuleProcedure()
                    {
                        ProcedureId = 1,
                        ProcedureName = "QueryBooks",
                    }
                }
            };
            var parsedResponse = RequestModuleResponse.Parser.ParseFrom(response);
            Assert.True(expectedResponse.Equals(parsedResponse));      
            
            Assert.True(clientWrapper.GetMessagesCount() == 0);
        }

        [Test]
        public void ShouldPortBeCreated()
        {
            var clientWrapper = new TransportAsyncWrapper(client);
            CreatePort(clientWrapper);
        }
        
        [Test]
        public void ShouldModuleBeLoaded()
        {
            var clientWrapper = new TransportAsyncWrapper(client);
            CreatePort(clientWrapper);

            LoadModule(clientWrapper);
        }

        public void SetupTestEnvironment(TransportAsyncWrapper clientWrapper)
        {
            CreatePort(clientWrapper);

            LoadModule(clientWrapper);
        }

        [Test]
        public void ShouldServiceResponse()
        {
            var clientWrapper = new TransportAsyncWrapper(client);
            SetupTestEnvironment(clientWrapper);
            
            var payload = new GetBookRequest()
            {
                Isbn =  7669 // mr pato
            };
            var request = new Request()
            {
                MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                    RpcMessageTypes.Request,
                    2
                ),
                PortId = 1,
                Payload = payload.ToByteString(),
                ProcedureId = 0 // GetBook
            };
            client.SendMessage(request.ToByteArray());

            var response = clientWrapper.GetNextMessage().GetAwaiter().GetResult();
            var expectedResponse = new Response
            {
                MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                    RpcMessageTypes.Response,
                    2
                ),
                Payload = new Book
                {
                    Author = "mr pato",
                    Isbn = 7669,
                    Title = "Base64 fan"
                }.ToByteString()
            };
            var parsedResponse = Response.Parser.ParseFrom(response);
            Assert.True(expectedResponse.Equals(parsedResponse));
            
            Assert.True(clientWrapper.GetMessagesCount() == 0);
        }
        
        [Test]
        public void ShouldServiceResponseStreamMessages()
        {
            var clientWrapper = new TransportAsyncWrapper(client);
            SetupTestEnvironment(clientWrapper);
            
            var payload = new QueryBooksRequest()
            {
                AuthorPrefix = "mr"
            };
            var request = new Request()
            {
                MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                    RpcMessageTypes.Request,
                    2
                ),
                PortId = 1,
                Payload = payload.ToByteString(),
                ProcedureId = 1 // QueryBooks
            };
            client.SendMessage(request.ToByteArray());

            {
                var response = clientWrapper.GetNextMessage().GetAwaiter().GetResult();
                var expectedResponse = new StreamMessage
                {
                    MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                        RpcMessageTypes.StreamMessage,
                        2
                    ),
                    Ack = false,
                    SequenceId = 0,
                    Closed = false,
                    PortId = 1
                };
                var parsedResponse = StreamMessage.Parser.ParseFrom(response);
                Assert.True(expectedResponse.Equals(parsedResponse));
            }

            // Client StreamMessage ACK
            TestUtils.ClientSendStreamMessageAck(client, 2, 1, 0);
            
            // Accumulate answers
            for (uint i = 0; i < context.books.Length; ++i)
            {
                TestUtils.ClientSendStreamMessageAck(client, 2, 1, i+1);
            }

            for (uint i = 0; i < context.books.Length; ++i)
            {
                var streamResponse = clientWrapper.GetNextMessage().GetAwaiter().GetResult();
                var expectedStreamResponse = new StreamMessage
                {
                    MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                        RpcMessageTypes.StreamMessage,
                        2
                    ),
                    PortId = 1,
                    Ack = false,
                    SequenceId = i+1,
                    Payload = context.books[i].ToByteString()
                };
                var parsedStreamResponse = StreamMessage.Parser.ParseFrom(streamResponse);
                Assert.True(expectedStreamResponse.Equals(parsedStreamResponse));
            }
            
            // Get Close Stream Message
            {
                var response = clientWrapper.GetNextMessage().GetAwaiter().GetResult();
                var expectedResponse = new StreamMessage
                {
                    MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                        RpcMessageTypes.StreamMessage,
                        2
                    ),
                    Ack = false,
                    SequenceId = (uint)context.books.Length,
                    Closed = true,
                    PortId = 1
                };
                var parsedResponse = StreamMessage.Parser.ParseFrom(response);
                Assert.True(expectedResponse.Equals(parsedResponse));
            }
            
            Assert.True(clientWrapper.GetMessagesCount() == 0);
        }
    }
}