// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Script.Description
{
    // Each function can get its own log stream.
    // Static per-function logging information.
    public class FunctionLogger
    {
        public FunctionLogger(ILoggerFactory loggerFactory, string functionName, string logDirName = null)
        {
            Logger = loggerFactory?.CreateLogger(LogCategories.CreateFunctionCategory(functionName));
        }

        public ILogger Logger { get; private set; }

        public void TraceError(string errorMessage)
        {
            Logger?.LogError(errorMessage);
        }

        // Helper to emit a standard log message for function started.
        public void LogFunctionStart(string invocationId)
        {
            string startMessage = $"Function started (Id={invocationId})";
            Logger?.LogInformation(startMessage);
        }

        public void LogFunctionResult(bool success, string invocationId, long elapsedMs)
        {
            string resultString = success ? "Success" : "Failure";
            string message = $"Function completed ({resultString}, Id={invocationId ?? "0"}, Duration={elapsedMs}ms)";

            LogLevel logLevel = success ? LogLevel.Information : LogLevel.Error;
            Logger?.Log(logLevel, new EventId(0), message, null, (s, e) => s);
        }
    }
}
