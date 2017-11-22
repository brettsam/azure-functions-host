﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Script.IO;
using WebJobs.Script.Tests;
using Xunit;

namespace Microsoft.Azure.WebJobs.Script.Tests.IO
{
    public class AutoRecoveringFileSystemWatcherTests
    {
        [Fact]
        public void SubdirectoryChanges_WithIncludeSubdirectories_SendsNotification()
        {
            using (var directory = new TempDirectory())
            using (var subDirectory = new TempDirectory(Path.Combine(directory.Path, "directory")))
            {
                string filePath = Path.Combine(subDirectory.Path, "file.txt");

                Action<AutoRecoveringFileSystemWatcher> action = w => File.WriteAllText(filePath, "test");
                Func<FileSystemEventArgs, bool> handler = a => string.Equals(a.FullPath, filePath, StringComparison.OrdinalIgnoreCase) && a.ChangeType == WatcherChangeTypes.Created;

                FileWatcherTest(directory.Path, action, handler);
            }
        }

        [Fact]
        public void Created_SendsExpectedNotification()
        {
            FileChanges_SendsExpectedNotification(WatcherChangeTypes.Created);
        }

        [Fact]
        public void Change_SendsExpectedNotification()
        {
            FileChanges_SendsExpectedNotification(WatcherChangeTypes.Changed);
        }

        [Fact]
        public void Rename_SendsExpectedNotification()
        {
            FileChanges_SendsExpectedNotification(WatcherChangeTypes.Renamed);
        }

        [Fact]
        public void Delete_SendsExpectedNotification()
        {
            FileChanges_SendsExpectedNotification(WatcherChangeTypes.Deleted);
        }

        [Fact]
        public async Task WhenFailureIsDetected_FileWatcherAutoRecovers()
        {
            await RecoveryTest(4, false);
        }

        [Fact(Skip = "Test failing in CI. Needs investigation")]
        public async Task AutoRecovery_StopsWhenDisposed()
        {
            await RecoveryTest(4, true);
        }

        public async Task RecoveryTest(int expectedNumberOfAttempts, bool isFailureScenario)
        {
            using (var directory = new TempDirectory())
            using (var watcher = new AutoRecoveringFileSystemWatcher(directory.Path))
            {
                Directory.Delete(directory.Path, true);

                string fileWatcherLogSuffix = $"(path: '{directory.Path}')";

                // 1 recovery initiating trace + 1 trace per attempt + 1 trace per failed attempt
                int expectedTracesBeforeRecovery = 1 + ((expectedNumberOfAttempts - 1) * 2);

                // Non failure: expected traces + last attempt + recovery trace
                // Failure: expected traces + abort/failure trace
                int expectedTracesAfterRecovery = expectedTracesBeforeRecovery + (isFailureScenario ? 1 : 2);

                await TestHelpers.Await(() =>
                {
                    return traceWriter.Traces.Count == expectedTracesBeforeRecovery;
                }, pollingInterval: 500);

                if (isFailureScenario)
                {
                    watcher.Dispose();
                }
                else
                {
                    Directory.CreateDirectory(directory.Path);
                }

                await TestHelpers.Await(() =>
                {
                    return traceWriter.Traces.Count == expectedTracesAfterRecovery;
                }, pollingInterval: 500);

                TraceEvent failureEvent = traceWriter.Traces.First();
                Assert.Equal(TraceLevel.Warning, failureEvent.Level);
                Assert.Contains("Failure detected", failureEvent.Message);

                var retryEvents = traceWriter.Traces.Where(t => t.Level == TraceLevel.Warning).Skip(1).ToList();

                if (isFailureScenario)
                {
                    // If this is a failed scenario, we've aborted before the last attempt
                    expectedNumberOfAttempts--;
                }

                Assert.Equal(expectedNumberOfAttempts, retryEvents.Count);

                // Validate that our the events happened with the expected intervals
                DateTime previoustTimeStamp = failureEvent.Timestamp;
                for (int i = 0; i < retryEvents.Count; i++)
                {
                    long expectedInterval = Convert.ToInt64((Math.Pow(2, i + 1) - 1) / 2);
                    TraceEvent currentEvent = retryEvents[i];

                    var actualInterval = currentEvent.Timestamp - previoustTimeStamp;
                    previoustTimeStamp = currentEvent.Timestamp;

                    int intervalInSeconds = (int)Math.Round(actualInterval.TotalSeconds, 0, MidpointRounding.ToEven);
                    Assert.True(expectedInterval == intervalInSeconds,
                        $"Recovering interval did not meet the expected interval (expected '{expectedInterval}', actual '{intervalInSeconds}");
                }

                Assert.True(traceWriter.Traces.All(t => t.Message.EndsWith(fileWatcherLogSuffix)));

                if (isFailureScenario)
                {
                    Assert.Contains("Recovery process aborted.", traceWriter.Traces.Last().Message);
                }
                else
                {
                    Assert.Contains("File watcher recovered.", traceWriter.Traces.Last().Message);
                }

                Assert.Equal(ScriptConstants.TraceSourceFileWatcher, traceWriter.Traces.Last().Source);
            }
        }

        public void FileChanges_SendsExpectedNotification(WatcherChangeTypes changeType)
        {
            using (var directory = new TempDirectory())
            {
                string filePath = Path.Combine(directory.Path, "file.txt");

                Action<AutoRecoveringFileSystemWatcher> action = w =>
                {
                    // create
                    File.WriteAllText(filePath, "test");

                    // update
                    File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow);

                    // Rename
                    string movePath = Path.ChangeExtension(filePath, "tst");
                    File.Move(filePath, movePath);
                    File.Move(movePath, filePath);

                    // Delete
                    File.Delete(filePath);
                };

                Func<FileSystemEventArgs, bool> handler = a => string.Equals(a.FullPath, filePath, StringComparison.OrdinalIgnoreCase) && a.ChangeType == changeType;

                FileWatcherTest(directory.Path, action, handler);
            }
        }

        public void FileWatcherTest(string path, Action<AutoRecoveringFileSystemWatcher> action, Func<FileSystemEventArgs, bool> changeHandler,
            WatcherChangeTypes changeTypes = WatcherChangeTypes.All, bool expectEvent = true)
        {
            var traceWriter = new TestTraceWriter(System.Diagnostics.TraceLevel.Verbose);

            using (var watcher = new AutoRecoveringFileSystemWatcher(path, traceWriter: traceWriter))
            {
                var resetEvent = new ManualResetEventSlim();

                watcher.Changed += (s, a) =>
                {
                    if (changeHandler(a))
                    {
                        resetEvent.Set();
                    }
                };

                action(watcher);

                bool eventSignaled = resetEvent.Wait(TimeSpan.FromSeconds(5));

                Assert.Equal(expectEvent, eventSignaled);
            }
        }

        private class TestFileSystemWatcher : AutoRecoveringFileSystemWatcher
        {
            public TestFileSystemWatcher(string path, string filter = "*.*",
                bool includeSubdirectories = true, WatcherChangeTypes changeTypes = WatcherChangeTypes.All, TraceWriter traceWriter = null)
                : base(path, filter, includeSubdirectories, changeTypes, traceWriter)
            {
            }

            internal void TriggerHandleError(ErrorEventArgs args)
            {
                OnFileWatcherError(args);
            }
        }
    }
}
