using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Assignment.CustomTreadPoolAndTaskScheduler
{
    public class CustomThreadPoolWithBackgroundWorker : ICustomThreadPool
    {
        private readonly ILogger _logger;

        /// <summary>
        /// defines the thread count limit, 
        /// above which there is 500 milliseconds penalty before the next thread is created
        /// </summary>
        private readonly int _slowDownCount;
        private readonly int _slowDownPenalty = 500;

        /// <summary>
        /// slowEvenMoreCount is thread count limit, 
        /// above which there is 20 seconds penalty before the next thread is created
        /// </summary>
        private readonly int _slowEvenMoreCount;
        private readonly int _slowEvenMorePenalty = 20 * 1000;// As first one in mili second so converting this one to milisecond.

        private bool _disposed;
        private readonly List<BackgroundWorker> _existingWorkers;
        private readonly object _existingWorkersLockObject;

        public CustomThreadPoolWithBackgroundWorker(ILogger logger,
            int slowDownCount,
            int slowEvenMoreCount)
        {
            _logger = logger;
            _slowDownCount = slowDownCount;
            _slowEvenMoreCount = slowEvenMoreCount;
            _disposed = false;
            _existingWorkers = new List<BackgroundWorker>();
            _existingWorkersLockObject = new object();
        }


        [MethodImpl(MethodImplOptions.Synchronized)]
        public void StartAction(Action action)
        {
            var totalCount = _existingWorkers.Count;

            if (totalCount > _slowEvenMoreCount)
            {
                _logger.Warning($"We are trying to schedule more than the slow Even More count { _slowEvenMoreCount } " +
             $"and total count is {totalCount}. So We are going to face slow even more penalty that is {_slowEvenMorePenalty} ms. ");

                Thread.Sleep(_slowEvenMorePenalty);
            }
            else if (totalCount > _slowDownCount)
            {
                _logger.Warning($"We are trying to schedule more than the slow down count { _slowDownCount } " +
              $"and total count is {totalCount}. So We are going to face slow down penalty that is {_slowDownPenalty} ms. ");

                Thread.Sleep(_slowDownPenalty);
            }


            var thread = new BackgroundWorker();
            // A thread is either a background thread or a foreground thread. Background threads are identical to foreground threads,
            //except that background threads do not prevent a process from terminating.Once all foreground threads belonging to a process
            //have terminated, the common language runtime ends the process. Any remaining background threads are stopped and do not complete.

            thread.DoWork += Thread_DoWork;
            thread.RunWorkerCompleted += Thread_RunWorkerCompleted;
            thread.RunWorkerAsync(action);
            _existingWorkers.Add(thread);

            _logger.Information($"Current thread count { _existingWorkers.Count }");
        }

        private void Thread_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                if (e.Argument is Action action)
                {
                    action();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "An exception occured while excuting Action.");
            }
        }
        private void Thread_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (sender is BackgroundWorker backgroundWorker)
            {
                lock (_existingWorkersLockObject)
                    _existingWorkers.Remove(backgroundWorker);
            }

            _logger.Information($"Current thread count { _existingWorkers.Count }");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _existingWorkers.ForEach(x =>
                {
                    x.CancelAsync();
                    x.Dispose();
                });
                _existingWorkers.Clear();
            }

            _disposed = true;
        }
    }
}
