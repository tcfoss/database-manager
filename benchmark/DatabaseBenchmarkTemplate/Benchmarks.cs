using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace DatabaseBenchmarkTemplate;

#pragma warning disable CA1822 // Member does not access instance data

[MemoryDiagnoser]
public class Benchmarks
{
    private readonly ConfigSelector _configSelector = ConfigSelector.Windows;
    private IServiceScope _scope = null!;


    [GlobalSetup]
    public void Setup()
    {
        IServiceProvider services = AppStarter.GetStartedApp(_configSelector, configureServices: ConfigureServices);
        _scope = services.CreateScope();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _scope.Dispose();
    }


    [Params(false, true)]
    public bool UseNewImplementation { get; set; }

    // Placeholder: replace this with the operation being measured.
    [Benchmark]
    public Task RunBenchmark()
    {
        return Task.CompletedTask;
    }

    private void ConfigureServices(IServiceCollection services)
    {
    }
}
