using Serilog;
using Serilog.Events;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Assignment.CustomTreadPoolAndTaskScheduler
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello there!" + Environment.NewLine);
            Console.WriteLine("I am going to show you an example of CustomTreadPoolAndTaskScheduler." + Environment.NewLine);

            #region Logger configuration

            var appDirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                                   "CustomTreadPoolAndTaskScheduler");
            if (!Directory.Exists(appDirectoryPath))
                Directory.CreateDirectory(appDirectoryPath);

            Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            // .WriteTo.Console() You can enable console logging from here
            .WriteTo.File(
                @$"{appDirectoryPath}\log.txt",
                fileSizeLimitBytes: 1_000_000,
                rollOnFileSizeLimit: true,
                shared: true,
                flushToDiskInterval: TimeSpan.FromSeconds(1))
            .CreateLogger();
            //      Yes, we can configure the log by using the JSON file and configuration builder.
            //and generally, we follow that approach but for demo purpose, I am using this one.
            #endregion


            ICustomThreadPool customThreadPool;
            // I have not created a factory for getting the ICustomThreadPool instance so using a flag
            // for switching between CustomThreadPoolWithBackgroundWorker and CustomThreadPool.
            // By default CustomThreadPool class will be used for demonstration
            // but you can use this flag to switch it for testing
            var testCustomThreadPoolWithBackgroundWorker = false;
            if (testCustomThreadPoolWithBackgroundWorker)
            {
                customThreadPool = new CustomThreadPoolWithBackgroundWorker(Log.Logger,
                    slowDownCount: 20,
                    slowEvenMoreCount: 50);
            }
            else
            {
                customThreadPool = new CustomThreadPool(Log.Logger,
                    slowDownCount: 50,
                    slowEvenMoreCount: 100);
            }

            // Task Scheduler 
            var customTaskScheduler = new CustomTaskScheduler(Log.Logger, customThreadPool);

            var random = new Random();


            try
            {
                // On this path, we will be creating a directoy and some files,
                // I have choosen Desktop so we can easily delete it.
                var directoryPath = Path.Combine(appDirectoryPath, Guid.NewGuid().ToString());

                // This function will be used in next test scenarios
                // If you want to add or change something then this is the best place 
                // Also you can use this to increase/decrease the processing 
                Action<string> createAndSaveFile = (directoryPath) =>
                {
                    if (string.IsNullOrEmpty(directoryPath)) return;
                    if (!Directory.Exists(directoryPath))
                        Directory.CreateDirectory(directoryPath);
                    // Guid.NewGuid() generating a new guid is going to cost me but I am here for ......
                    File.WriteAllText(Path.Combine(directoryPath, Guid.NewGuid().ToString() + ".txt"),
                        @"                  

.NET Core is an open-source, general-purpose development platform maintained by
Microsoft and the .NET community on GitHub. It's cross-platform (supporting Windows, macOS, and Linux) 
and can be used to build device, cloud, and IoT applications.

See About .NET Core to learn more about .NET Core, including its characteristics, supported languages
and frameworks, and key APIs.

Check out .NET Core Tutorials to learn how to create a simple .NET Core application. It only takes a 
few minutes to get your first app up and running. If you want to try .NET Core in your browser, 
look at the Numbers in C# online tutorial.
");
                };

                /* Why I have used the above string ?
                   1. Because it will make some sense.
                   2. Easily available
                   3. A long string
                 */

                // Based on Parallel.For **
                FirstTestScenario(customTaskScheduler, createAndSaveFile, Log.Logger, directoryPath);

                // Based on Task.Factory  **
                // SecondTestScenario(customTaskScheduler, createAndSaveFile, Log.Logger, directoryPath);


            }
            catch (Exception ex)
            {
                Log.Error(ex, "An exception while excuting the Main.");
            }
            finally
            {
                customTaskScheduler.Dispose();
                customThreadPool.Dispose();
                Log.CloseAndFlush();
            }

            Console.WriteLine(" Done. (For more details please go through with the log file.) ");
            Console.Read();
        }

        static void FirstTestScenario(CustomTaskScheduler customTaskScheduler,
           Action<string> customFunction, ILogger logger, string directorypath)
        {
            ParallelOptions options = new ParallelOptions
            {
                TaskScheduler = customTaskScheduler
            };

            Parallel.For(0, 35000, options, (index) =>
            {
                try
                {
                    customFunction(directorypath);
                    Console.WriteLine(" Current Index :" + index);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "An exception occurred.");
                }
            });
        }

        static void SecondTestScenario(CustomTaskScheduler customTaskScheduler,
            Action<string> customFunction, ILogger logger, string directorypath)
        {

            var task = Task.Factory.StartNew(() =>
            {
                for (int index = 0; index < 35000; index++)
                {
                    try
                    {
                        customFunction(directorypath);
                        Console.WriteLine(" Current Index :" + index);
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "An exception occurred.");
                    }
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, customTaskScheduler);
            task.RunSynchronously();
            // Using Wait is a very bad practice with async and await, I am using RunSynchronously here
            // for demo purpose and we are not having async and await :) 
        }
    }

}
