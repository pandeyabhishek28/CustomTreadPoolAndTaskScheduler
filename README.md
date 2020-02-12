# CustomTreadPoolAndTaskScheduler
CustomTreadPoolAndTaskScheduler

The default .Net ThreadPool has some flaws: application can freeze in some scenarios due to thread pool starvation and 
you cannot monitor how many threads are actually running. 
Therefore it is hard to identify and react to problems related to thread resources.

There are two classes: CustomThreadPool and CustomTaskScheduler. CustomTaskScheduler will use the CustomThreadPool internally.

#CustomTaskScheduler class:

•	Is inheriting the TaskScheduler abstract class (from .Net)

•	Purpose of the class is to be used on input by Parallel.ForEach() and Task.Factory.StartNew() functions

•	Uses CustomTreadPool internally

•	Has one thread that schedules the incoming tasks onto CustomThreadPool immediately when task arrives

•	Is lazy: until the first task arrives there is nothing running inside
•	Is thread-safe

•	Is disposable: frees resources when disposed
•	Takes CustomThreadPool and ILogger on input in constructor

#CustomThreadPool class:

•	Is instance type –A different approach in comparison to the default ThreadPool
•	Constructor takes 3 arguments: ILogger logger, int slowDownCount, int slowEvenMoreCount
o	logger(serilog)
o	slowDownCount defines the thread count limit, above which there is 500 milliseconds penalty before the next thread is created
o	slowEvenMoreCount is thread count limit, above which there is 20 seconds penalty before the next thread is created
•	Until limit slowDownCount is reached each thread is created immediately
•	Thread pool starts with zero worker threads, each thread is created ad hoc
•	Thread pool is lazy
•	Thread pool is fully thread safe
•	Thread pool is disposable
•	No thread in thread pool runs to the end (only if the Threadpool is disposed)
o	If there is any available thread in thread pool, the action execution starts immediately
o	If there is no available thread, it creates a new one. If we are already above slowDownCount limit, the delay penalties apply in sequence.
o	CustomThreadPool.StartAction() method is the method used internally by CustomTaskScheduler
•	ThreadPool monitors number of available and total threads
o	For allocations above slowDownCount reports warnings and errors reasonably
o	Once in some reasonable period it will reports current available/total counts

A few very basic tests are there so that we can see it's working as expected or not.

