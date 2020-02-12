
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Assignment.CustomTreadPoolAndTaskScheduler
{
    public class CustomTaskScheduler : TaskScheduler, IDisposable
    {
        private readonly ILogger _logger;
        private readonly ICustomThreadPool _customThreadPool;
        private readonly BlockingCollection<Task> _tasks;
        private bool _disposed;
        private Thread _schedulerThread;
        private readonly CancellationTokenSource cancellationTokenSource;
        public CustomTaskScheduler(ILogger logger,
            ICustomThreadPool customThreadPool)
        {
            _logger = logger;
            _customThreadPool = customThreadPool;
            _disposed = false;
            _tasks = new BlockingCollection<Task>();
            cancellationTokenSource = new CancellationTokenSource();
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return _tasks.ToArray();
        }

        protected override void QueueTask(Task task)
        {
            if (task == null) return;
            _logger.Information("One task has arrived into Queue Task.");

            if (_schedulerThread == null)
            {
                _schedulerThread = new Thread(() => ScheduleTaskOverThreadPool(cancellationTokenSource.Token))
                {
                    IsBackground = true
                };

                _schedulerThread.Start();
            }

            _tasks.Add(task);

            _logger.Information("The task has been added into the collection.");
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            if (task == null) return false;

            if (taskWasPreviouslyQueued)
                return false;

            if ((task.IsCompleted == false ||
                   task.Status == TaskStatus.Running ||
                   task.Status == TaskStatus.WaitingToRun ||
                   task.Status == TaskStatus.WaitingForActivation))
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        private void ScheduleTaskOverThreadPool(CancellationToken token)
        {

            if (token.IsCancellationRequested) return;
            _logger.Information("Start scheduling.");

            foreach (var task in _tasks.GetConsumingEnumerable())
            {
                if (token.IsCancellationRequested) break;
                _customThreadPool.StartAction(() => TryExecuteTask(task));
            }

            _logger.Information("Done scheduling.");
        }

        public void Dispose()
        {
            _logger.Information("The Dispose is called.");

            Dispose(true);
            GC.SuppressFinalize(this);

            _logger.Information("The Dispose is completed.");
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                cancellationTokenSource.Cancel();
                _logger.Information("Disposing..........");
                // Indicate that no new tasks will be coming in
                _tasks.CompleteAdding();
                // _schedulerThread.Abort()
                // The Thread.Abort method is not supported in .NET Core. If you need to terminate the execution of third - party code 
                // forcibly in .NET Core, run it in the separate process and use Process.Kill.
                _customThreadPool.Dispose();
                _schedulerThread.Join();
                _tasks.Dispose();
                cancellationTokenSource.Dispose();
            }

            _disposed = true;
        }
    }
}
