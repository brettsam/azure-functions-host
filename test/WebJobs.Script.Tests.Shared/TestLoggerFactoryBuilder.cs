// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Script;
using Microsoft.Azure.WebJobs.Script.Config;
using Microsoft.Extensions.Logging;

namespace Microsoft.WebJobs.Script.Tests
{
    public class TestLoggerFactoryBuilder : ILoggerFactoryBuilder
    {
        private readonly TestLoggerProvider _loggerProvider;

        public TestLoggerFactoryBuilder(TestLoggerProvider loggerProvider)
        {
            _loggerProvider = loggerProvider;
        }

        public void AddLoggerProviders(ILoggerFactory factory, ScriptHostConfiguration scriptConfig, ScriptSettingsManager settingsManager, Func<bool> fileLoggingEnabled, Func<bool> isPrimaryHost)
        {
            factory.AddProvider(_loggerProvider);
        }
    }
}
