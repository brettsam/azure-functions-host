// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Script.WebHost;
using Microsoft.Azure.WebJobs.Script.WebHost.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Extensions.Logging
{
    public static class ILoggingBuilderExtensions
    {
        public static void AddWebJobsSystem<T>(this ILoggingBuilder builder) where T : SystemLoggerProvider
        {
            builder.Services.AddSingleton<ILoggerProvider, T>();

            // Log all logs to SystemLogger
            builder.AddDefaultWebJobsFilters<T>(LogLevel.Trace);
        }

        public static void AddDeferred(this ILoggingBuilder builder)
        {
            builder.Services.AddSingleton<DeferredLoggerProvider>();

            // The ASP.NET host startup will ask for loggers, but not all services are ready,
            // so this will be null. The runtime startup will correctly populate the services.
            builder.Services.AddSingleton<ILoggerProvider>(s =>
            {
                var environment = s.GetService<IScriptWebHostEnvironment>();
                if (environment != null)
                {
                    return new DeferredLoggerProvider(environment);
                }

                return NullLoggerProvider.Instance;
            });

            builder.Services.AddSingleton<IDeferredLogSource>(s => s.GetRequiredService<DeferredLoggerProvider>());

            // Do not filter this. It will be filtered by the consumer of the IDeferredLogSource.
            builder.AddFilter<DeferredLoggerProvider>(_ => true);
        }
    }
}