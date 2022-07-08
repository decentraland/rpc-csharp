using rpc_csharp_demo.example;

namespace rpc_csharp_demo
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            ServerExample.Run();
            NodeClientExample.Run("rpc-test-client-js");
        }
    }
}