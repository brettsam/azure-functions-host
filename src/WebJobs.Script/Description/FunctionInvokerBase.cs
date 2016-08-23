// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Script.Diagnostics;

namespace Microsoft.Azure.WebJobs.Script.Description
{
    public abstract class FunctionInvokerBase : IFunctionInvoker, IDisposable
    {
        private const string PrimaryHostTracePropertyName = "PrimaryHost";
        private readonly static IDictionary<string, object> _primaryHostTraceProperties = new Dictionary<string, object> { { PrimaryHostTracePropertyName, null } };

        private FileSystemWatcher _fileWatcher;
        private bool _disposed = false;
        private IMetricsLogger _metrics;

        internal FunctionInvokerBase(ScriptHost host, FunctionMetadata functionMetadata, ITraceWriterFactory traceWriterFactory = null)
        {
            Host = host;
            Metadata = functionMetadata;

            traceWriterFactory = traceWriterFactory ?? new FunctionTraceWriterFactory(functionMetadata.Name, Host.ScriptConfig);
            TraceWriter traceWriter = traceWriterFactory.Create();

            // Function file logging is only done conditionally
            TraceWriter = traceWriter.Conditional(t => Host.FileLoggingEnabled && (!(t.Properties?.ContainsKey(PrimaryHostTracePropertyName) ?? false) || Host.IsPrimary));
            _metrics = host.ScriptConfig.HostConfig.GetService<IMetricsLogger>();
        }

        protected static IDictionary<string, object> PrimaryHostTraceProperties => _primaryHostTraceProperties;

        public ScriptHost Host { get; private set; }

        public FunctionMetadata Metadata { get; private set; }

        public TraceWriter TraceWriter { get; }

        /// <summary>
        /// All unhandled invocation exceptions will flow through this method.
        /// We format the error and write it to our function specific <see cref="TraceWriter"/>.
        /// </summary>
        /// <param name="ex"></param>
        public virtual void OnError(Exception ex)
        {
            string error = Utility.FlattenException(ex);

            TraceError(error);
        }

        protected virtual void TraceError(string errorMessage)
        {
            TraceWriter.Error(errorMessage);

            // when any errors occur, we want to flush immediately
            TraceWriter.Flush();
        }

        protected bool InitializeFileWatcherIfEnabled()
        {
            if (Host.ScriptConfig.FileWatchingEnabled)
            {
                string functionDirectory = Path.GetDirectoryName(Metadata.ScriptFile);
                _fileWatcher = new FileSystemWatcher(functionDirectory, "*.*")
                {
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true
                };
                _fileWatcher.Changed += OnScriptFileChanged;
                _fileWatcher.Created += OnScriptFileChanged;
                _fileWatcher.Deleted += OnScriptFileChanged;
                _fileWatcher.Renamed += OnScriptFileChanged;

                return true;
            }

            return false;
        }

        public Task Invoke(object[] parameters)
        {
            FunctionStartedEvent startedEvent = null;
            Task invokeTask = null;

            CancellationToken timeoutOrShutdownToken = (CancellationToken)parameters.First(p => p?.GetType() == typeof(CancellationToken));
            ExecutionContext functionExecutionContext = (ExecutionContext)parameters.First(p => p?.GetType() == typeof(ExecutionContext));
            string invocationId = functionExecutionContext.InvocationId.ToString();

            startedEvent = new FunctionStartedEvent(functionExecutionContext.InvocationId, Metadata);
            _metrics.BeginEvent(startedEvent);

            try
            {
                TraceWriter.Info($"Function started (Id={invocationId})");

                invokeTask = InvokeInternal(parameters);
                invokeTask.Wait(timeoutOrShutdownToken);

                TraceWriter.Info($"Function completed (Success, Id={invocationId})");
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == timeoutOrShutdownToken)
            {
                string reason = "Host is Stopping";

                // TODO: What's the best way to do this?
                var stateProp = typeof(JobHost).GetField("_state", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                int hostState = (int)stateProp.GetValue(Host);

                // 3 indicates StoppingOrStopped. If that's the case, just let it go.
                // Otherwise it's a timeout.
                if (hostState != 3)
                {
                    reason = "Timeout";
                    ex.Data.Add("FunctionTimeout", true);
                }

                if (startedEvent != null)
                {
                    startedEvent.Success = false;
                }

                LogFunctionFailed($"Failure: {reason}", invocationId);

                throw;
            }
            catch (AggregateException ex)
            {
                AggregateException flattenedEx = ex.Flatten();
                ExceptionDispatchInfo exInfo = null;

                // If there's only a single exception, rethrow it by itself
                if (flattenedEx.InnerExceptions.Count == 1)
                {
                    exInfo = ExceptionDispatchInfo.Capture(flattenedEx.InnerExceptions.Single());
                }
                else
                {
                    exInfo = ExceptionDispatchInfo.Capture(flattenedEx);
                }

                if (startedEvent != null)
                {
                    startedEvent.Success = false;
                }

                LogFunctionFailed("Failure", invocationId);

                exInfo.Throw();
            }
            catch
            {
                if (startedEvent != null)
                {
                    startedEvent.Success = false;
                }

                LogFunctionFailed("Failure", invocationId);

                throw;
            }
            finally
            {
                if (startedEvent != null)
                {
                    _metrics.EndEvent(startedEvent);
                }
            }

            return Task.FromResult(0);
        }

        private void LogFunctionFailed(string resultString, string invocationId)
        {
            string message = $"Function completed ({resultString}";
            if (string.IsNullOrEmpty(invocationId))
            {
                TraceWriter.Error($"{message})");
            }
            else
            {
                TraceWriter.Error($"{message}, Id={invocationId})");
            }
        }

        public abstract Task InvokeInternal(object[] parameters);

        protected virtual void OnScriptFileChanged(object sender, FileSystemEventArgs e)
        {
        }

        protected void TraceOnPrimaryHost(string message, TraceLevel level)
        {
            TraceWriter.Trace(message, level, PrimaryHostTraceProperties);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _fileWatcher?.Dispose();

                    (TraceWriter as IDisposable)?.Dispose();
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
