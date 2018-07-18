﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using DryIoc;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.WebJobs.Script.WebHost.DependencyInjection
{
    public class FunctionsServiceScope : IServiceScope
    {
        private readonly ScopedServiceProvider _serviceProvider;

        public FunctionsServiceScope(IResolverContext serviceProvider)
        {
            _serviceProvider = new ScopedServiceProvider(serviceProvider);
        }

        public IServiceProvider ServiceProvider => _serviceProvider;

        public void Dispose()
        {
            _serviceProvider.Dispose();
        }
    }
}
