using System.Diagnostics;

namespace rpc_csharp_demo.example;

static class NodeClientExample
{
    public static void Run(string workingDirectory)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "node",
                    Arguments = "index.js",
                    UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDirectory
                }
            };

            process.Start();

            while (!process.StandardOutput.EndOfStream)
            {
                var line = process.StandardOutput.ReadLine();
                Console.WriteLine(line);
            }
            
            while (!process.StandardError.EndOfStream)
            {
                var line = process.StandardError.ReadLine();
                Console.WriteLine(line);
            }

            process.WaitForExit();
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }
}