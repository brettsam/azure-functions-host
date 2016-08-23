// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Xunit;

namespace Microsoft.Azure.WebJobs.Script.Tests
{
    public class EndToEndTimeoutTests
    {
        [Fact]
        public async Task TimeoutTest_SyncFunction_Node()
        {
            await TimeoutTest_SyncFunction("Node");
        }

        [Fact]
        public async Task TimeoutTest_SyncFunction_Bash()
        {
            await TimeoutTest_SyncFunction("Bash");
        }

        [Fact]
        public async Task TimeoutTest_SyncFunction_Batch()
        {
            await TimeoutTest_SyncFunction("WindowsBatch");
        }

        [Fact]
        public async Task TimeoutTest_SyncFunction_Python()
        {
            await TimeoutTest_SyncFunction("Python");
        }

        [Fact]
        public async Task TimeoutTest_SyncFunction_Powershell()
        {
            await TimeoutTest_SyncFunction("PowerShell");
        }

        [Fact]
        public async Task TimeoutTest_SyncFunction_CSharp()
        {
            await TimeoutTest_SyncFunction("CSharp");
        }

        private async Task TimeoutTest_SyncFunction(string scriptLang)
        {
            await RunTimeoutTest(scriptLang, "TimeoutSync");
        }

        [Fact]
        public async Task TimeoutTest_UsingToken_CSharp()
        {
            await RunTokenTest("useToken", (logs) =>
             {
                 // The function should 'clean up' and write 'Done'
                 logs.Single(l => l.EndsWith("Done"));
             });
        }

        [Fact]
        public async Task TimeoutTest_IgnoringToken_CSharp()
        {
            await RunTokenTest("ignoreToken", (logs) =>
             {
                 // We do not expect 'Done' to be written here.
                 Assert.False(logs.Any(l => l.EndsWith("Done")));
             });
        }

        private async Task RunTokenTest(string scenario, Action<IList<string>> verify)
        {
            string functionName = "TimeoutToken";
            TestHelpers.ClearFunctionLogs(functionName);

            using (var manager = await CreateAndStartScriptHostManager("CSharp", functionName, TimeSpan.FromSeconds(3)))
            {
                Dictionary<string, object> arguments = new Dictionary<string, object>
                {
                    { "input", scenario },
                };

                var ex = await Assert.ThrowsAsync<OperationCanceledException>(() => manager.Instance.CallAsync(functionName, arguments));

                Assert.True(manager.IsTimeout);
                var logs = await TestHelpers.GetFunctionLogsAsync(functionName);
                verify(logs);
            }
        }

        [Fact]
        public async Task StopHost_DoesNotThrowTimeout_CSharp()
        {
            string functionName = "TimeoutSync";
            TestHelpers.ClearFunctionLogs(functionName);

            var manager = await CreateAndStartScriptHostManager("CSharp", functionName, TimeSpan.FromMinutes(5));
            string data = Guid.NewGuid().ToString();

            Dictionary<string, object> arguments = new Dictionary<string, object>
                {
                    { "input", data },
                };

            Task t = manager.Instance.CallAsync(functionName, arguments);

            await TestHelpers.Await(() =>
            {
                var inProgressLogs = TestHelpers.GetFunctionLogsAsync(functionName, throwOnNoLogs: false).Result;
                return inProgressLogs?.Any(l => l.Contains(data)) ?? false;
            });

            // now stop the manager
            manager.Stop();
            manager.Dispose();

            var ex = await Assert.ThrowsAsync<TaskCanceledException>(async () => await t);

            await TestHelpers.Await(() =>
            {
                var inProgressLogs = TestHelpers.GetFunctionLogsAsync(functionName, throwOnNoLogs: false).Result;
                bool completedTimeout = inProgressLogs?.Any(l => l.Contains("Function completed (Failure: Host is Stopping")) ?? false;
                Assert.False(inProgressLogs.Any(l => l.ToLowerInvariant().Contains("timeout")));
                return completedTimeout;
            });

            Assert.False(manager.IsTimeout);
        }

        protected async Task RunTimeoutTest(string scriptLang, string functionName)
        {
            TestHelpers.ClearFunctionLogs(functionName);
            CloudQueue queue = GetQueueReference();

            using (var manager = await CreateAndStartScriptHostManager(scriptLang, functionName, TimeSpan.FromSeconds(3)))
            {
                string testData = Guid.NewGuid().ToString();
                await queue.AddMessageAsync(new CloudQueueMessage(testData));

                // Wait for a timeout to be logged
                await TestHelpers.Await(() =>
                {
                    IList<string> inProgressLogs = TestHelpers.GetFunctionLogsAsync(functionName, throwOnNoLogs: false).Result;
                    var match = inProgressLogs.SingleOrDefault(l => l.Contains("Function completed (Failure: Timeout"));
                    return match != null;
                });

                var logs = await TestHelpers.GetFunctionLogsAsync(functionName);
                // make sure logging from within the function worked
                Assert.True(logs.Any(l => l.Contains(testData)));
                Assert.True(manager.IsTimeout);
            }
        }

        private CloudQueue GetQueueReference()
        {
            string connectionString = AmbientConnectionStringProvider.Instance.GetConnectionString(ConnectionStringNames.Storage);
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();

            CloudQueue queue = queueClient.GetQueueReference("test-input-timeout");
            queue.CreateIfNotExists();
            queue.Clear();
            return queue;
        }

        protected async Task<MockScriptHostManager> CreateAndStartScriptHostManager(string scriptLang, string functionName, TimeSpan timeout)
        {
            var functions = new Collection<string>();
            functions.Add(functionName);

            ScriptHostConfiguration config = new ScriptHostConfiguration()
            {
                RootScriptPath = $@"TestScripts\{scriptLang}",
                TraceWriter = new TestTraceWriter(TraceLevel.Verbose),
                FileLoggingMode = FileLoggingMode.Always,
                Functions = functions
            };
            config.HostConfig.FunctionTimeout = timeout;

            var scriptHostManager = new MockScriptHostManager(config);
            ThreadPool.QueueUserWorkItem((s) => scriptHostManager.RunAndBlock());
            await TestHelpers.Await(() => scriptHostManager.IsRunning);

            return scriptHostManager;
        }

        public class MockScriptHostManager : ScriptHostManager
        {
            public MockScriptHostManager(ScriptHostConfiguration config) : base(config)
            {
            }

            public bool IsTimeout { get; private set; } = false;

            protected override void OnTimeout()
            {
                IsTimeout = true;
                base.OnTimeout();
            }
        }
    }
}
