using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using rpc_csharp.transport;

namespace rpc_csharp_test
{
    public class MemoryTransportTest
    {
        private ITransport client;
        private ITransport server;

        [SetUp]
        public void Setup()
        {
            (client, server) = MemoryTransport.Create();
        }

        [Test]
        public async Task ShouldSwapMessages()
        {
            var clientWrapper = new TransportAsyncWrapper(client);
            var serverWrapper = new TransportAsyncWrapper(server);
            
            server.SendMessage(new byte[] { 123 });
            Assert.True((await clientWrapper.GetNextMessage()).SequenceEqual(new byte[] { 123 }));
            server.SendMessage(new byte[] { 10, 35, 80 });
            Assert.True((await clientWrapper.GetNextMessage()).SequenceEqual(new byte[] { 10, 35, 80 }));
            server.SendMessage(new byte[] { 45, 120, 99, 255, 0 });
            Assert.True((await clientWrapper.GetNextMessage()).SequenceEqual(new byte[] { 45, 120, 99, 255, 0 }));
            
            client.SendMessage(new byte[] { 123 });
            Assert.True((await serverWrapper.GetNextMessage()).SequenceEqual(new byte[] { 123 }));
            client.SendMessage(new byte[] { 10, 35, 80 });
            Assert.True((await serverWrapper.GetNextMessage()).SequenceEqual(new byte[] { 10, 35, 80 }));
            client.SendMessage(new byte[] { 45, 120, 99, 255, 0 });
            Assert.True((await serverWrapper.GetNextMessage()).SequenceEqual(new byte[] { 45, 120, 99, 255, 0 }));
        }
    }
}