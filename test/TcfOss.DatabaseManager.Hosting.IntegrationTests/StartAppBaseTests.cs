using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.Configuration.Parsing;
using TcfOss.DatabaseManager.Core.Hosting;
using TcfOss.DatabaseManager.Core.IntegrationTests;

namespace TcfOss.DatabaseManager.Hosting.IntegrationTests;

[CollectionDefinition(nameof(HostStartupCollection), DisableParallelization = true)]
public sealed class HostStartupCollection;

[Collection(nameof(HostStartupCollection))]
public class StartAppBaseTests(FsProjectFixture fixture) : IClassFixture<FsProjectFixture>
{
    private FsProjectFixture Fixture { get; } = fixture;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Start_ResultDisposesResolvedServices(bool asynchronously)
    {
        using var result = new FakeStartApp().Start(workingDirectory: CreateWorkingDirectory(), options: new StartupOptions
        {
            ConfigureServices = services => services.AddSingleton<StubService>()
        });
        var service = result.Services.GetRequiredService<StubService>();

        if (asynchronously)
        {
            await result.DisposeAsync();
        }
        else
        {
            result.Dispose();
        }

        Assert.True(service.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => result.Services.GetService<StubService>());
    }

    [Fact]
    public async Task Start_ResultAsynchronouslyDisposesAsyncOnlyServices()
    {
        AsyncOnlyStubService service;
        await using (var result = new FakeStartApp().Start(workingDirectory: CreateWorkingDirectory(), options: new StartupOptions
        {
            ConfigureServices = services => services.AddSingleton<AsyncOnlyStubService>()
        }))
        {
            service = result.Services.GetRequiredService<AsyncOnlyStubService>();
        }

        Assert.True(service.IsDisposed);
    }

    [Fact]
    public void Start_DisposesHostWhenPostBuildInitializationFails()
    {
        StubService? service = null;
        IServiceProvider? provider = null;
        var failure = new InvalidOperationException("Startup failed.");
        var app = new FakeStartApp(services =>
        {
            provider = services;
            service = services.GetRequiredService<StubService>();
            throw failure;
        });

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => app.Start(
            workingDirectory: CreateWorkingDirectory(),
            options: new StartupOptions
            {
                ConfigureServices = services => services.AddSingleton<StubService>()
            })));

        Assert.NotNull(service);
        Assert.True(service.IsDisposed);
        Assert.NotNull(provider);
        Assert.Throws<ObjectDisposedException>(() => provider.GetService<StubService>());
    }

    [Fact]
    public void Start_ExposesServicesAndAppliesCustomizationHooks()
    {
        var app = new FakeStartApp();

        using var result = app.Start(workingDirectory: CreateWorkingDirectory(), options: new StartupOptions
        {
            ConfigureBuilder = builder => builder.Services.AddSingleton<IStubService>(new StubService()),
            ConfigureServices = services => services.AddSingleton<IStubService>(new StubService())
        });

        Assert.NotNull(result.Services);
        Assert.NotNull(result.Config);
        Assert.NotNull(result.ServiceProvider);
        Assert.NotNull(result.Services.GetRequiredService<IStubService>());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" with ' quote")]
    public void Start_UsesExplicitConfigFile_WhenProvided(string suffix)
    {
        var tempDir = CreateWorkingDirectory(suffix);
        var configPath = Path.Combine(tempDir, "database-manager.yaml");
        File.WriteAllText(configPath, "dialect: generic\nprojectDirectory: '" + tempDir.Replace("'", "''") + "'\n");

        var app = new FakeStartApp();
        using var result = app.Start(configFilePath: configPath, workingDirectory: tempDir);

        Assert.False(result.UsingDefaultConfig);
        Assert.Equal(tempDir, result.Config.ProjectDirectory);
    }

    [Fact]
    public void Start_UsesDefaultConfig_WhenNoConfigFileExists()
    {
        var tempDir = CreateWorkingDirectory();
        var app = new FakeStartApp();
        using var result = app.Start(workingDirectory: tempDir);

        Assert.True(result.UsingDefaultConfig);
        Assert.Equal(tempDir, result.Config.ProjectDirectory);
        Assert.NotNull(result.Services);
    }

    [Fact]
    public void Start_UsesProvidedRawConfig_WithoutLoadingFile()
    {
        var tempDir = CreateWorkingDirectory();
        var app = new FakeStartApp();
        var rawConfig = new Config
        {
            ProjectDirectory = tempDir,
            Catalog = "def",
            Dialect = SqlDialect.Generic,
            Schemas = []
        };

        using var result = app.Start(rawConfig: rawConfig, workingDirectory: tempDir);

        Assert.False(result.UsingDefaultConfig);
        Assert.Equal(tempDir, result.Config.ProjectDirectory);
    }

    private string CreateWorkingDirectory(string suffix = "")
    {
        string path = Fixture.CombinePath(Guid.NewGuid().ToString("N") + suffix);
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FakeStartApp(Action<IServiceProvider>? configureApp = null) : StartAppBase<FakeStartResult>
    {
        protected override SqlDialect Dialect => SqlDialect.Generic;

        public override FakeStartResult Start(string? configFilePath = null, Config? rawConfig = null, string? workingDirectory = null, bool relaxed = false, StartupOptions? options = null)
        {
            var startResult = StartCommon(configFilePath, rawConfig, workingDirectory, relaxed, options);
            return new FakeStartResult(startResult.Host)
            {
                Config = (GenericConfig)startResult.Config,
                ServiceProvider = new AppServiceProvider(),
                UsingDefaultConfig = startResult.UsingDefaultConfig
            };
        }

        protected override StartupBase GetStartup()
        {
            return new FakeStartup(configureApp);
        }
    }

    private sealed class FakeStartup(Action<IServiceProvider>? configureApp) : StartupBase
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
            EnvironmentVariableReaderBase environmentVariableReader,
            Func<IHostApplicationBuilder, LogSettings, (ILogger, string?)> loggingSetupFunc,
            Action<ILogger, string?>? logSetupAction = null,
            Action<ConfigBase>? configSetupAction = null,
            bool relaxed = false)
        {
            (ILogger logger, _) = loggingSetupFunc(builder, rawConfig.Logging);
            logSetupAction?.Invoke(logger, null);

            var config = new GenericConfig
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
            configureApp?.Invoke(serviceProvider);
        }
    }

    private sealed record FakeStartResult(IHost host) : StartResultBase(host), IStartResult<GenericConfig, AppServiceProvider>
    {
        public required GenericConfig Config { get; init; }
        public required AppServiceProvider ServiceProvider { get; init; }
        public required bool UsingDefaultConfig { get; init; }
    }

    private interface IStubService;

    private sealed class AsyncOnlyStubService : IAsyncDisposable
    {
        public bool IsDisposed { get; private set; }

        public async ValueTask DisposeAsync()
        {
            await Task.Yield();
            IsDisposed = true;
        }
    }

    private sealed class StubService : IStubService, IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
