using System;
using System.Diagnostics;

class NodeClientExample
{
    public static void run(string WorkingDirectory)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "node",
                    Arguments = "dist/integration.js",
                    UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = WorkingDirectory
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