using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.Configuration.Parsing;
using TcfOss.DatabaseManager.Core.DatabaseComms;
using TcfOss.DatabaseManager.MariaDb.LibWrapper;
using TcfOss.DatabaseManager.MySql.App;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;

namespace BenchmarkDeJoin
{
    [MemoryDiagnoser]
    public class Benchmarks
    {
        // Tracks EF Core "CommandExecuted" diagnostic events so we can see round-trip count/duration per iteration.
        private static long s_commandCount;
        private static long s_commandDurationTicks;

        [Params("true", "false")]
        public string NewMethod { get; set; } = "false";

        private ILoadDbDefinition<MyDefinition> _builder = null!;
        private IDisposable? _listenerSubscription;

        [GlobalSetup]
        public void Setup()
        {
            Environment.SetEnvironmentVariable("new_method", NewMethod);

            _listenerSubscription = DiagnosticListener.AllListeners.Subscribe(new EfCoreListenerObserver());

            var starter = new MariaDbStartApp();

            string realRootDir = @"C:\Users\flanagantc\source\repos\IleiDatabase4\database";
            var config = new Config
            {
                ProjectDirectory = realRootDir,
                Dialect = SqlDialect.MariaDb,
                Schemas = [
                    new SchemaMapping()
                    {
                        SchemaName = "devdb_migrate1",
                        RootPath = "Schema"
                    }
                ],
                Credentials = new Credentials
                {
                    Hostname = "127.0.0.1",
                    Username = "y_flanagantc",
                    Port = "3311"
                }
            };

            // string realRootDir = @"/home/thomas/Code/IssueTracker/database";
            // var config = new Config
            // {
            //     ProjectDirectory = realRootDir,
            //     Dialect = SqlDialect.MariaDb,
            //     Schemas = [
            //         new SchemaMapping()
            //         {
            //             SchemaName = "issue_tracking_test",
            //             RootPath = "Schema"
            //         }
            //     ],
            //     Credentials = new Credentials
            //     {
            //         Hostname = "localhost",
            //         Username = "thomas",
            //         SocketPath = "/var/run/mysqld/mysqld.sock"
            //     }
            // };

            starter.Start(rawConfig: config, workingDirectory: realRootDir);
            _builder = MyAppServiceProvider.DatabaseDefinitionLoader;
        }

        [IterationSetup]
        public void ResetCommandCounters()
        {
            Interlocked.Exchange(ref s_commandCount, 0);
            Interlocked.Exchange(ref s_commandDurationTicks, 0);
        }

        [Benchmark]
        public async Task LoadDefinition()
        {
            await _builder.LoadDefinitionAsync();
        }

        [IterationCleanup]
        public void ReportCommandCounters()
        {
            long count = Interlocked.Read(ref s_commandCount);
            TimeSpan duration = TimeSpan.FromTicks(Interlocked.Read(ref s_commandDurationTicks));
            Console.WriteLine($"[DbCommands] NewMethod={NewMethod} Count={count} TotalDuration={duration}");
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _listenerSubscription?.Dispose();
        }

        // Subscribes to the EF Core diagnostic source to observe command-executed events without touching app startup code.
        private sealed class EfCoreListenerObserver : IObserver<DiagnosticListener>
        {
            public void OnNext(DiagnosticListener value)
            {
                if (value.Name == "Microsoft.EntityFrameworkCore")
                {
                    value.Subscribe(new CommandExecutedObserver());
                }
            }

            public void OnCompleted()
            {
            }

            public void OnError(Exception error)
            {
            }
        }

        private sealed class CommandExecutedObserver : IObserver<KeyValuePair<string, object?>>
        {
            public void OnNext(KeyValuePair<string, object?> value)
            {
                if (!value.Key.EndsWith("CommandExecuted", StringComparison.Ordinal))
                {
                    return;
                }

                Interlocked.Increment(ref s_commandCount);

                if (value.Value?.GetType().GetProperty("Duration")?.GetValue(value.Value) is TimeSpan duration)
                {
                    Interlocked.Add(ref s_commandDurationTicks, duration.Ticks);
                }
            }

            public void OnCompleted()
            {
            }

            public void OnError(Exception error)
            {
            }
        }
    }
}