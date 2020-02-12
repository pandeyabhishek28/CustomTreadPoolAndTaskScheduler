using System;

namespace Assignment.CustomTreadPoolAndTaskScheduler
{
    public interface ICustomThreadPool : IDisposable
    {
        public void StartAction(Action action);
    }
}
