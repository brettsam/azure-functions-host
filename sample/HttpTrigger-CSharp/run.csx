using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

public static async Task<IActionResult> Run(HttpRequest req, TraceWriter log)
{
    await Task.Yield();

    log.Info("C# HTTP trigger function processed a request.");

    using (StreamReader reader = new StreamReader(req.Body))
    {
        string readText = reader.ReadToEnd();
        Console.WriteLine(readText); 
    }

    if (req.Query.TryGetValue("name", out StringValues value))
    {
        return new OkObjectResult($"Hello, {value.ToString()}");
    }

    return new BadRequestObjectResult("Please pass a name on the query string or in the request body");
}