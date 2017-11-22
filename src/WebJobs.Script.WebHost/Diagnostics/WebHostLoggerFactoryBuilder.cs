// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.


using Microsoft.Azure.WebJobs.Script.Config;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Script.WebHost.Diagnostics
{
    public class WebHostLoggerFactoryBuilder : ILoggerFactoryBuilder
    {
        private IEventGenerator _eventGenerator;
        private ScriptSettingsManager _settingsManager;

        public WebHostLoggerFactoryBuilder(IEventGenerator eventGenerator, ScriptSettingsManager settingsManager)
        {
            _eventGenerator = eventGenerator;
            _settingsManager = settingsManager;
        }

        public void AddLoggerProviders(ILoggerFactory factory)
        {
            factory.AddProvider(new SystemLoggerProvider(_eventGenerator, _settingsManager));
        }
    }
}
