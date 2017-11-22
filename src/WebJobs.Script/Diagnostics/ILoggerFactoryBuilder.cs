// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Script
{
    /// <summary>
    /// Provides ways to plug into the ScriptHost ILoggerFactory initialization.
    /// </summary>
    public interface ILoggerFactoryBuilder
    {
        /// <summary>
        /// Adds additional <see cref="ILoggerProvider"/>s to the <see cref="ILoggerFactory"/>.
        /// </summary>
        /// <param name="factory">The <see cref="ILoggerFactory"/>.</param>
        void AddLoggerProviders(ILoggerFactory factory);
    }
}
