using System;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using rpc_csharp_demo.example;

namespace rpc_csharp_demo
{
    internal class Program
    {
        private static IUniTaskAsyncEnumerable<int> firstGenerator()
        {
            return UniTaskAsyncEnumerable.Create<int>(async (writer, token) =>
            {
                for (int i = 0; i < 1000; ++i)
                {
                    if (token.IsCancellationRequested) break;
                    Console.WriteLine($"firstGenerator: {i}");
                    await writer.YieldAsync(i);
                }
            });
        }
        
        private static IUniTaskAsyncEnumerable<int> middleGenerator(IUniTaskAsyncEnumerable<int> generator)
        {
            return UniTaskAsyncEnumerable.Create<int>(async (writer, token) =>
            {
                await foreach(var number in generator)
                {
                    if (token.IsCancellationRequested) break;
                    Console.WriteLine($"middleGenerator: {number}");
                    await writer.YieldAsync(-number);
                }
            });
        }

        private static async UniTask asyncCall()
        {
            await foreach (var negativeNumber in middleGenerator(firstGenerator()))
            {
                Console.WriteLine($"resultGenerator: {negativeNumber}");
                if (negativeNumber == -500) break;
            }
        }
        public static void Main(string[] args)
        {
            //while(!asyncCall().GetAwaiter().IsCompleted) {}
            ServerExample.Run();
            //NodeClientExample.Run("rpc-test-client-js");
        }
    }
}