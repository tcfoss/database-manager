using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using ParserBenchmarks;

if (args.SequenceEqual(["--validate"]))
{
    var benchmarks = new MySqlParsingBenchmarks();
    benchmarks.Setup();
    int tokens = benchmarks.LexerOnly();
    int statements = benchmarks.EndToEnd();
    if (benchmarks.ParserOnly() != statements || benchmarks.ParserOnly() != statements
        || benchmarks.EndToEnd() != statements || benchmarks.LexerOnly() != tokens)
    {
        throw new InvalidOperationException("Repeated benchmark operations produced inconsistent counts.");
    }

    Console.WriteLine($"Validated {tokens:N0} tokens and {statements:N0} statements per corpus pass.");
    return;
}

string artifactsPath = Path.Combine(
    "/home/thomas/Desktop/ParserBenchmarks",
    DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", System.Globalization.CultureInfo.InvariantCulture));
IConfig config = DefaultConfig.Instance.WithArtifactsPath(artifactsPath);
BenchmarkSwitcher.FromAssembly(typeof(MySqlParsingBenchmarks).Assembly).Run(args, config);