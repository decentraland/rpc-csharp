using System;
using System.IO;
using NUnit.Framework;
using rpc_csharp.example;

namespace rpc_csharp_test
{
    [TestFixture]
    public class Tests
    {
        [Test]
        public void Test1()
        {
            ServerExample.run();
            
            NodeClientExample.run("rpc-test-client-js");
            Assert.True(true);
        }
    }
}