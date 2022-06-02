using System;
using NUnit.Framework;

namespace rpc_csharp_test
{
    [TestFixture]
    public class Tests
    {
        [Test]
        public void Test1()
        {
            Console.Write("Hello from test");
            Assert.True(true);
        }
    }
}