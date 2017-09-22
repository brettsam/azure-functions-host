// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Script.Diagnostics
{
    internal class LoggerFactoryWrapper : ILoggerFactory
    {
        private ILoggerFactory _loggerFactory = new LoggerFactory();
        private bool _disposed = false;
        private TraceWriter _trace;

        public LoggerFactoryWrapper(TraceWriter trace)
        {
            _trace = trace;
        }

        public void AddProvider(ILoggerProvider provider)
        {
            LogWarningIfDisposed();
            _loggerFactory.AddProvider(provider);
        }

        public ILogger CreateLogger(string categoryName)
        {
            LogWarningIfDisposed();
            return _loggerFactory.CreateLogger(categoryName);
        }

        private void LogWarningIfDisposed()
        {
            _trace.Warning("Attempting to access a disposed LoggerFactory.");
        }

        public void Dispose()
        {
            // Don't actually dispose. This is for diagnostic purposes
            // until we can isolate all the cases where we are using
            // a disposed LoggerFactory.
            _disposed = true;
        }
    }
}
