using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Script.Diagnostics
{
    internal class FileLogger : ILogger
    {
        private readonly FileWriter _fileWriter;
        private readonly Func<string, LogLevel, bool> _filter;
        private readonly Func<bool> _isPrimary;
        private readonly string _categoryName;

        public FileLogger(string categoryName, FileWriter fileWriter, Func<string, LogLevel, bool> filter, Func<bool> isPrimary)
        {
            _fileWriter = fileWriter;
            _filter = filter;
            _isPrimary = isPrimary;
            _categoryName = categoryName;
        }

        public IDisposable BeginScope<TState>(TState state) => DictionaryLoggerScope.Push(state);

        public bool IsEnabled(LogLevel logLevel)
        {
            if (_filter == null)
            {
                // if there is no filter, assume it is always enabled
                return true;
            }

            return _filter(_categoryName, logLevel);
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            IEnumerable<KeyValuePair<string, object>> stateValues = state as IEnumerable<KeyValuePair<string, object>>;
            string formattedMessage = formatter?.Invoke(state, exception);

            // If we don't have a message or any key/value pairs, there's nothing to log.
            if (stateValues == null && string.IsNullOrEmpty(formattedMessage))
            {
                return;
            }

            if (!IsEnabled(logLevel))
            {
                return;
            }

            bool isSystemTrace = GetStateBoolValue(stateValues, ScriptConstants.TracePropertyIsSystemTraceKey);
            if (isSystemTrace)
            {
                // System traces are not logged to files.
                return;
            }

            bool isPrimaryHostTrace = GetStateBoolValue(stateValues, ScriptConstants.TracePropertyPrimaryHostKey);
            if (isPrimaryHostTrace && !_isPrimary())
            {
                return;
            }

            _fileWriter.AppendLine(FormatMessage(formattedMessage));

            // flush errors immediately
            if (logLevel == LogLevel.Error || exception != null)
            {
                _fileWriter.Flush();
            }
        }

        private string FormatMessage(string message)
           => string.Format(CultureInfo.InvariantCulture, "{0} {1}", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture), message.Trim());

        private static bool GetStateBoolValue(IEnumerable<KeyValuePair<string, object>> state, string key)
        {
            if (state == null)
            {
                return false;
            }

            var kvps = state.Where(k => string.Equals(k.Key, key, StringComparison.OrdinalIgnoreCase));

            if (!kvps.Any())
            {
                return false;
            }

            // Choose the last one rather than throwing for multiple hits. Since we use our own keys to track
            // this, we shouldn't have conflicts.
            return Convert.ToBoolean(kvps.Last().Value);
        }
    }
}
