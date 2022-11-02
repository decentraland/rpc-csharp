using System.Linq;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Google.Protobuf;
using NUnit.Framework;
using rpc_csharp;
using rpc_csharp_demo.example;
using rpc_csharp.protocol;
using rpc_csharp.transport;

namespace rpc_csharp_test
{
    public class RpcServerTest
    {
        private ITransport client;
        private ITransport server;
        private BookContext context;

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
                BookServiceCodeGen.RegisterService(port, new BookServiceImpl());
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
                    },
                    new ModuleProcedure()
                    {
                        ProcedureId = 2,
                        ProcedureName = "GetBookStream",
                    },
                    new ModuleProcedure()
                    {
                        ProcedureId = 3,
                        ProcedureName = "QueryBooksStream",
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
                Isbn = 7669 // mr pato
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
        public void ShouldServiceResponseStreamMessagesCase1()
        {
            /* Open stream, ask for all messages, and wait for the close event from the server
             */
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
                TestUtils.ClientSendStreamMessageAck(client, 2, 1, i + 1);
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
                    SequenceId = i + 1,
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
                    SequenceId = (uint) context.books.Length,
                    Closed = true,
                    PortId = 1
                };
                var parsedResponse = StreamMessage.Parser.ParseFrom(response);
                Assert.True(expectedResponse.Equals(parsedResponse));
            }

            Assert.True(clientWrapper.GetMessagesCount() == 0);
        }

        [Test]
        public void ShouldServiceResponseStreamMessagesCase2()
        {
            /* Open stream, and close it immediatly */
        }

        [Test]
        public void ShouldServiceResponseStreamMessagesCase3()
        {
            /* Open stream, ask for two messages, and close from the client */
        }

        [Test]
        public async UniTask ShouldServiceResponseStreamMessagesCase4()
        {
            /* Open stream, ask for two messages, and close from the client */
            context.books = new[]
            {
                null,
                null,
                null,
                null,
                new Book() {Author = "mr pato", Isbn = 7669, Title = "Base64 fan"},
            };
            
            var clientWrapper = new TransportAsyncWrapper(client);
            SetupTestEnvironment(clientWrapper);
            Assert.True(clientWrapper.GetMessagesCount() == 0);

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
                var response = await clientWrapper.GetNextMessage();
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

            var streamResponse = await clientWrapper.GetNextMessage();
            //Assert.True(clientWrapper.GetMessagesCount() == 0);
            //var streamResponse = clientWrapper.GetNextMessage().GetAwaiter().GetResult();
            var expectedStreamResponse = new StreamMessage
            {
                MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                    RpcMessageTypes.StreamMessage,
                    2
                ),
                PortId = 1,
                Ack = false,
                SequenceId = 1,
                Payload = context.books.Last().ToByteString()
            };
            var parsedStreamResponse = StreamMessage.Parser.ParseFrom(streamResponse);
            Assert.True(expectedStreamResponse.Equals(parsedStreamResponse));

            // Request one answer
            TestUtils.ClientSendStreamMessageAck(client, 2, 1, 1);
            
            // Try to request another one... but it should close
            //TestUtils.ClientSendStreamMessageAck(client, 2, 1, 2);

            // Get Close Stream Message
            {
                var response = await clientWrapper.GetNextMessage();
                var expectedResponse = new StreamMessage
                {
                    MessageIdentifier = ProtocolHelpers.CalculateMessageIdentifier(
                        RpcMessageTypes.StreamMessage,
                        2
                    ),
                    Ack = false,
                    SequenceId = (uint) 1,
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