// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Azure.WebJobs.Script.Config;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Script.WebHost.Diagnostics
{
    public class SystemLogger : ILogger
    {
        private readonly bool _isFunctionUserCategory = false;
        private IEventGenerator _eventGenerator;
        private ScriptSettingsManager _settingsManager;
        private string _appName;
        private string _subscriptionId;
        private string _categoryName;
        private static readonly Regex _userFunctionRegex = new Regex(@"^Function\.[^\s]+\.User");

        public SystemLogger(string categoryName, IEventGenerator eventGenerator, ScriptSettingsManager settingsManager)
        {
            _settingsManager = settingsManager;
            _appName = _settingsManager.AzureWebsiteUniqueSlotName;
            _subscriptionId = Utility.GetSubscriptionId();
            _eventGenerator = eventGenerator;
            _categoryName = categoryName;
            _isFunctionUserCategory = _userFunctionRegex.IsMatch(_categoryName);
        }

        public IDisposable BeginScope<TState>(TState state) => DictionaryLoggerScope.Push(state);

        public bool IsEnabled(LogLevel logLevel)
        {
            // Never log a message that came from the User logs.
            return !_isFunctionUserCategory;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            string formattedMessage = formatter?.Invoke(state, exception);

            // If we don't have a message, there's nothing to log.
            if (string.IsNullOrEmpty(formattedMessage))
            {
                return;
            }

            // Apply standard event properties
            // Note: we must be sure to default any null values to empty string
            // otherwise the ETW event will fail to be persisted (silently)
            string subscriptionId = _subscriptionId ?? string.Empty;
            string appName = _appName ?? string.Empty;
            string source = _categoryName ?? string.Empty;
            string summary = Sanitizer.Sanitize(formattedMessage) ?? string.Empty;

            // Apply any additional extended event info from the scope
            string functionName = string.Empty;
            string eventName = string.Empty;
            string details = string.Empty;

            IDictionary<string, object> scopeProps = DictionaryLoggerScope.GetMergedStateDictionary();

            if (scopeProps != null)
            {
                if (scopeProps.TryGetValue(ScopeKeys.FunctionName, out object value) && value != null)
                {
                    functionName = value.ToString();
                }
            }

            if (string.IsNullOrEmpty(details) && exception != null)
            {
                details = Sanitizer.Sanitize(exception.ToFormattedString());
            }

            _eventGenerator.LogFunctionTraceEvent(logLevel, subscriptionId, appName, functionName, eventName, source, details, summary);
        }
    }
}