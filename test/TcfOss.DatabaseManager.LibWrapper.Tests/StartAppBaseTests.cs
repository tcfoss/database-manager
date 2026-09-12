using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.Configuration.Parsing;
using TcfOss.DatabaseManager.Core.LibWrapper;

namespace TcfOss.DatabaseManager.LibWrapper.Tests;

public class StartAppBaseTests
{
    [Fact]
    public void Start_ExposesServicesAndAppliesCustomizationHooks()
    {
        var app = new FakeStartApp();

        var result = app.Start(workingDirectory: Path.GetTempPath(), options: new StartupOptions
        {
            ConfigureBuilder = builder => builder.Services.AddSingleton<IStubService>(new StubService()),
            ConfigureServices = services => services.AddSingleton<IStubService>(new StubService())
        });

        Assert.NotNull(result.Services);
        Assert.NotNull(result.Config);
        Assert.NotNull(result.ServiceProvider);
        Assert.NotNull(result.Services.GetRequiredService<IStubService>());
    }

    private sealed class FakeStartApp : StartAppBase<FakeStartResult>
    {
        protected override SqlDialect Dialect => SqlDialect.Generic;

        public override FakeStartResult Start(string? configFilePath = null, Config? rawConfig = null, string? workingDirectory = null, bool relaxed = false, StartupOptions? options = null)
        {
            var startResult = StartCommon(configFilePath, rawConfig, workingDirectory, relaxed, options);
            return new FakeStartResult
            {
                Config = (ConfigGeneric)startResult.Config,
                ServiceProvider = new AppServiceProvider(),
                Services = startResult.Services,
                UsingDefaultConfig = startResult.UsingDefaultConfig
            };
        }

        protected override StartupBase GetStartup()
        {
            return new FakeStartup();
        }
    }

    private sealed class FakeStartup : StartupBase
    {
        public override Config GetDefaultConfig(string rootDirectory)
        {
            return new Config
            {
                ProjectDirectory = rootDirectory,
                Catalog = "def",
                Dialect = SqlDialect.Generic,
                Schemas = []
            };
        }

        public override void Configure(
            string rootDirectory,
            Config rawConfig,
            IHostApplicationBuilder builder,
            IReadEnvironmentVariables environmentVariableReader,
            Func<IHostApplicationBuilder, LogSettings, (ILogger, string?)> loggingSetupFunc,
            Action<ILogger, string?>? logSetupAction = null,
            Action<ConfigBase>? configSetupAction = null,
            bool relaxed = false)
        {
            var (logger, _) = loggingSetupFunc(builder, rawConfig.Logging);
            logSetupAction?.Invoke(logger, null);

            var config = new ConfigGeneric
            {
                ProjectDirectory = rootDirectory,
                Catalog = new Core.Common.CatalogIdentifier("def"),
                DatabaseAvailable = true,
                ValidationSettings = new ValidationSettings(),
                Dialect = SqlDialect.Generic
            };

            configSetupAction?.Invoke(config);
        }

        public override void ConfigureAppServiceProvider(IServiceProvider serviceProvider)
        {
            AppServiceProvider.ServiceProvider = serviceProvider;
        }
    }

    private sealed record FakeStartResult : IStartResult<ConfigGeneric, AppServiceProvider>
    {
        public required ConfigGeneric Config { get; init; }
        public required AppServiceProvider ServiceProvider { get; init; }
        public required IServiceProvider Services { get; init; }
        public required bool UsingDefaultConfig { get; init; }
    }

    private interface IStubService;

    private sealed class StubService : IStubService;
}
