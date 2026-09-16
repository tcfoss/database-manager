using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using DatabaseBenchmarkTemplate;

var config = DefaultConfig.Instance;
_ = BenchmarkRunner.Run<Benchmarks>(config, args);

// Use this to select benchmarks from the console:
// var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
