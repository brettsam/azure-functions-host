// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Script.Diagnostics
{
    internal class FileLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentDictionary<string, FileWriter> _fileWriterCache = new ConcurrentDictionary<string, FileWriter>(StringComparer.OrdinalIgnoreCase);
        private readonly Func<string, LogLevel, bool> _filter;
        private readonly Func<bool> _isPrimary;
        private readonly string _roogLogPath;
        private static readonly NullLogger _nullLogger = new NullLogger();

        private bool _disposed = false;

        public FileLoggerProvider(string rootLogPath, Func<string, LogLevel, bool> filter, Func<bool> isPrimary)
        {
            _roogLogPath = rootLogPath;
            _filter = filter;
            _isPrimary = isPrimary;
        }

        public ILogger CreateLogger(string categoryName)
        {
            string filePath = GetFilePath(categoryName);

            if (filePath != null)
            {
                // Make sure that we return the same fileWriter if multiple loggers write to the same path. This happens
                // with Function logs as Function.{FunctionName} and Function.{FunctionName}.User both go to the same file.
                FileWriter fileWriter = _fileWriterCache.GetOrAdd(filePath, (p) => new FileWriter(Path.Combine(_roogLogPath, filePath)));
                return new FileLogger(categoryName, fileWriter, _filter, _isPrimary);
            }

            return _nullLogger;
        }

        internal static string GetFilePath(string categoryName)
        {
            string filePath = null;

            // Only write to files for these category types. Each has slightly different
            // rules for how to generate the file path.
            if (categoryName == "Structured")
            {
                filePath = "Structured";
            }
            else if (categoryName.StartsWith("Worker."))
            {
                // Worker.{Language}.{Id} -> Worker\{Language}\{Id}
                string[] parts = categoryName.Split('.');
                if (parts.Length == 3)
                {
                    filePath = Path.Combine(parts);
                }
            }
            else if (categoryName.StartsWith("Function."))
            {
                // Function and Function.User logs go to the same file.
                // Function.{FunctionName} -> Function\{FunctionName}
                // Function.{FunctionName}.User -> Function\{FunctionName}
                string[] parts = categoryName.Split('.');
                if (parts.Length == 2 ||
                    (parts.Length == 3 && parts[2] == "User"))
                {
                    filePath = Path.Combine(parts[0], parts[2]);
                }
            }

            return filePath;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (IDisposable disposable in _fileWriterCache.Values)
                {
                    disposable.Dispose();
                }
                _disposed = true;
            }
        }

        private class NullLogger : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) => null;

            public bool IsEnabled(LogLevel logLevel) => false;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                // do nothing
            }
        }
    }
}
