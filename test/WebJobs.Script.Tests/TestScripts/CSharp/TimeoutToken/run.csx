using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;

public static async Task Run(string input, TraceWriter log, CancellationToken token)
{
    switch (input)
    {
        case "useToken":
            while (!token.IsCancellationRequested)
            {
            }
            break;
        case "ignoreToken":
            while (true)
            {
            }
            break;
        default:
            throw new InvalidOperationException($"'{input}' is an unknown command.");
    }

    log.Info("Done");
}