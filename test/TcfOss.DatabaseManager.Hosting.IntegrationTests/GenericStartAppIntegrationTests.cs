using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Hosting;
using TcfOss.DatabaseManager.Core.IntegrationTests;
using TcfOss.DatabaseManager.Core.Parsing;

namespace TcfOss.DatabaseManager.Hosting.IntegrationTests;

[Collection(nameof(HostStartupCollection))]
public class GenericStartAppIntegrationTests(FsProjectFixture fixture) : IClassFixture<FsProjectFixture>
{
    private FsProjectFixture Fixture { get; } = fixture;

    [Fact]
    public void GenericStartApp_DocumentationQuickStart_ParsesWithinScope()
    {
        string workingDirectory = CreateWorkingDirectory();
        var rawConfig = new GenericStartup().GetDefaultConfig(workingDirectory);
        using var result = new GenericStartApp().Start(rawConfig: rawConfig, workingDirectory: workingDirectory);

        using var scope = result.Services.CreateScope();
        var parser = scope.ServiceProvider.GetRequiredService<IParseText>();
        var statements = parser.ParseText("CREATE TABLE widgets (id INT);", filename: "widgets.sql");

        Assert.Single(statements);
        Assert.Equal(workingDirectory, result.Config.ProjectDirectory);
        Assert.False(result.UsingDefaultConfig);
    }

    [Fact]
    public void GenericStartApp_DocumentationCustomization_AppliesBothHooks()
    {
        string workingDirectory = CreateWorkingDirectory();
        var rawConfig = new GenericStartup().GetDefaultConfig(workingDirectory);
        using var result = new GenericStartApp().Start(
            rawConfig: rawConfig,
            workingDirectory: workingDirectory,
            options: new StartupOptions
            {
                ConfigureBuilder = builder => builder.Environment.ApplicationName = "MyApplication",
                ConfigureServices = services => services.AddSingleton(TimeProvider.System)
            });

        using var scope = result.Services.CreateScope();
        Assert.Same(TimeProvider.System, scope.ServiceProvider.GetRequiredService<TimeProvider>());
        Assert.Equal("MyApplication", scope.ServiceProvider.GetRequiredService<IHostEnvironment>().ApplicationName);
        Assert.Single(scope.ServiceProvider.GetRequiredService<IParseText>().ParseText("CREATE TABLE widgets (id INT);"));
    }

    [Fact]
    public void GenericServices_DocumentationManualHost_ParsesWithinScope()
    {
        string workingDirectory = CreateWorkingDirectory();
        var startup = new GenericStartup();
        var rawConfig = startup.GetDefaultConfig(workingDirectory);
        var config = startup.BuildConfiguration(
            workingDirectory, rawConfig, new EnvironmentVariableReader(),
            relaxed: false, logger: NullLogger.Instance);

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = workingDirectory,
            DisableDefaults = true
        });
        builder.Services.AddLogging();
        builder.RegisterCommonServices(config);
        builder.Services.RegisterGenericServices(config);

        using var host = builder.Build();
        using var scope = host.Services.CreateScope();
        var parser = scope.ServiceProvider.GetRequiredService<IParseText>();

        Assert.Single(parser.ParseText("CREATE TABLE widgets (id INT);"));
    }

    [Fact]
    public void GenericStartApp_ExposesServicesAndTypedConfig()
    {
        string workingDirectory = CreateWorkingDirectory();
        var app = new GenericStartApp();

        using var result = app.Start(workingDirectory: workingDirectory, options: new StartupOptions
        {
            ConfigureBuilder = builder => builder.Services.AddSingleton<ITestRegistration>(new TestRegistration())
        });

        Assert.NotNull(result.Services);
        Assert.IsType<GenericConfig>(result.Config);
        Assert.IsType<AppServiceProvider>(result.ServiceProvider);
        Assert.NotNull(result.Services.GetRequiredService<ITestRegistration>());
    }

    private string CreateWorkingDirectory()
    {
        string path = Fixture.CombinePath(Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private interface ITestRegistration;

    private sealed class TestRegistration : ITestRegistration;
}
