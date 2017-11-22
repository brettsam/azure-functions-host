// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Script.Diagnostics;
using Xunit;

namespace Microsoft.Azure.WebJobs.Script.Tests.Diagnostics
{
    public class FileLoggerProviderTests
    {
        [Theory]
        [InlineData("Worker.Java.12345", @"Worker\Java\12345")]
        [InlineData("Function.HttpTrigger", @"Function\HttpTrigger")]
        [InlineData("Function.HttpTrigger.User", @"Function\HttpTrigger")]
        [InlineData("Structured", @"Structured")]
        public void CreateLogger_GetsCorrectPaty(string category, string expectedPath)
        {
            Assert.Equal(expectedPath, FileLoggerProvider.GetFilePath(category));
        }
    }
}
