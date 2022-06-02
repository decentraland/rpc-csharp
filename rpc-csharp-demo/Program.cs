using rpc_csharp.example;

namespace rpc_csharp_demo
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            ServerExample.run();
            
            NodeClientExample.run("rpc-test-client-js");
        }
    }
}