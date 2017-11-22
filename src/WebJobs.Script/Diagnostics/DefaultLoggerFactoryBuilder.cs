// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using Microsoft.Azure.WebJobs.Script.Config;
using Microsoft.Azure.WebJobs.Script.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Script
{
    /// <summary>
    /// Provides ways to plug into the ScriptHost ILoggerFactory initialization.
    /// </summary>
    public class DefaultLoggerFactoryBuilder : ILoggerFactoryBuilder
    {
        private ScriptHostConfiguration _scriptConfig;
        private ScriptSettingsManager _settingsManager;
        private Func<bool> _isFileLoggingEnabled;
        private Func<bool> _isPrimaryHost;

        public DefaultLoggerFactoryBuilder(ScriptHostConfiguration scriptConfig, ScriptSettingsManager settingsManager, Func<bool> isFileLoggingEnabled, Func<bool> isPrimaryHost)
        {
            _scriptConfig = scriptConfig;
            _settingsManager = settingsManager;
            _isFileLoggingEnabled = isFileLoggingEnabled;
            _isPrimaryHost = isPrimaryHost;
        }

        /// <inheritdoc />
        public virtual void AddLoggerProviders(ILoggerFactory factory)
        {
            IMetricsLogger metricsLogger = _scriptConfig.HostConfig.GetService<IMetricsLogger>();

            // Automatically register App Insights if the key is present
            if (!string.IsNullOrEmpty(_settingsManager?.ApplicationInsightsInstrumentationKey))
            {
                metricsLogger?.LogEvent(MetricEventNames.ApplicationInsightsEnabled);

                ITelemetryClientFactory clientFactory = _scriptConfig.HostConfig.GetService<ITelemetryClientFactory>() ??
                    new ScriptTelemetryClientFactory(_settingsManager.ApplicationInsightsInstrumentationKey, _scriptConfig.LogFilter.Filter);

                _scriptConfig.HostConfig.LoggerFactory.AddApplicationInsights(clientFactory);
            }
            else
            {
                metricsLogger?.LogEvent(MetricEventNames.ApplicationInsightsDisabled);
            }

            factory.AddProvider(new FileLoggerProvider(_scriptConfig.RootLogPath, (category, level) => _isFileLoggingEnabled(), _isPrimaryHost));

            factory.AddConsole(_scriptConfig.LogFilter.DefaultLevel, true);
        }
    }
}