using Serilog;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Assignment.CustomTreadPoolAndTaskScheduler
{
    public class CustomThreadPool : IDisposable, ICustomThreadPool
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
        private readonly List<Thread> _existingThreads;
        private readonly CancellationTokenSource _cancellationTokenSource;
        public CustomThreadPool(ILogger logger,
            int slowDownCount,
            int slowEvenMoreCount)
        {
            _logger = logger;
            _slowDownCount = slowDownCount;
            _slowEvenMoreCount = slowEvenMoreCount;
            _disposed = false;
            _existingThreads = new List<Thread>();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void StartAction(Action action)
        {
            var thread = GetNewThread();
            thread.Start(action);

            _logger.Information($"Current thread count { _existingThreads.Count }");
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private Thread GetNewThread()
        {
            var totalCount = _existingThreads.Count;

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

            // A thread is either a background thread or a foreground thread. Background threads are identical to foreground threads,
            //except that background threads do not prevent a process from terminating.Once all foreground threads belonging to a process
            //have terminated, the common language runtime ends the process. Any remaining background threads are stopped and do not complete.

            var deadThreadCount = _existingThreads.RemoveAll(x => !x.IsAlive);
            if (deadThreadCount > 0)
            {
                _logger.Warning($"Clearing out the thread bucket by { deadThreadCount } threads, so next time we may avoid facing penalty.");
            }
            var thread = new Thread(ExcuteAction)
            {
                IsBackground = true
            };

            _existingThreads.Add(thread);

            return thread;
        }

        private void ExcuteAction(object data)
        {
            try
            {
                // Yes, there is a another way for passing the token with the excuting task
                // Just making it simple here
                if (_cancellationTokenSource.IsCancellationRequested) return;

                if (data is Action action)
                {
                    action();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "An exception occured while excuting Action.");
            }
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
                _cancellationTokenSource.Cancel();
                _existingThreads.ForEach(x =>
                {
                    // x.Abort();
                    // The Thread.Abort method is not supported in .NET Core. If you need to terminate the execution of 
                    // third - party code forcibly in .NET Core, run it in the separate process and use Process.Kill.
                    x.Join();
                });
                _existingThreads.Clear();
                _cancellationTokenSource.Dispose();
            }

            _disposed = true;
        }

        // Yes, I can abort each and every thread by following way but this does not seems right to me
        // and that's why I have created a another version of the CustomThreadPool, Please have a look for CustomThreadPoolWithBackgroundWorker.
        // Thank you........
        //[DllImport("kernel32.dll")]
        //static extern IntPtr OpenThread(uint dwDesiredAccess, bool bInheritHandle, uint dwThreadId);
        //[DllImport("kernel32.dll")]
        //static extern bool TerminateThread(IntPtr hThread, uint dwExitCode);
        //            var process = Process.GetCurrentProcess();
        //            foreach (ProcessThread item in process.Threads)
        //            {
        //                IntPtr handle = OpenThread((0x0001), false, (uint)item.Id);
        //                TerminateThread(handle, 0);
        //            }
    }
}
