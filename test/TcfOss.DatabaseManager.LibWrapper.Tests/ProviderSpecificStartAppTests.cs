using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.LibWrapper;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.MariaDb.LibWrapper;
using TcfOss.DatabaseManager.MsSql.Configuration;
using TcfOss.DatabaseManager.MsSql.Lexing;
using TcfOss.DatabaseManager.MsSql.LibWrapper;
using TcfOss.DatabaseManager.MsSql.Parsing;
using TcfOss.DatabaseManager.MySql.App;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.Lexing;
using TcfOss.DatabaseManager.MySql.LibWrapper;
using TcfOss.DatabaseManager.MySql.Parsing;

namespace TcfOss.DatabaseManager.LibWrapper.Tests;

public class ProviderSpecificStartAppTests
{
    [Theory]
    [InlineData("Generic")]
    [InlineData("MySql")]
    [InlineData("MsSql")]
    public void TextParser_DocumentationDirectConstruction_ParsesWithoutServices(string dialect)
    {
        var parser = dialect switch
        {
            "Generic" => new TextParser(new GenericLexer(), new Parser()),
            "MySql" => new TextParser(new MyLexer(), new MyParser()),
            "MsSql" => new TextParser(new MsLexer(), new MsParser()),
            _ => throw new ArgumentOutOfRangeException(nameof(dialect))
        };

        Assert.Single(parser.ParseText("CREATE TABLE widgets (id INT);", filename: "widgets.sql"));
    }

    [Fact]
    public void Formatter_DocumentationDirectConstruction_FormatsWithoutServices()
    {
        var workingDirectory = Directory.GetCurrentDirectory();
        var startup = new GenericStartup();
        var rawConfig = startup.GetDefaultConfig(workingDirectory);
        var config = startup.BuildConfiguration(
            workingDirectory, rawConfig, new EnvironmentVariableReader(),
            relaxed: false, logger: NullLogger.Instance);

        var parser = new TextParser(new GenericLexer(), new Parser());
        var functionNames = new FunctionNameProvider();
        using var formatter = new Formatter(config, parser, functionNames);

        var formatted = formatter.GetFormatted("create table widgets(id int);");

        Assert.Contains("CREATE TABLE", formatted);
        Assert.Single(parser.ParseText(formatted));
    }

    [Fact]
    public void GenericStartApp_DocumentationQuickStart_ParsesWithinScope()
    {
        var workingDirectory = Directory.GetCurrentDirectory();
        var rawConfig = new GenericStartup().GetDefaultConfig(workingDirectory);
        var result = new GenericStartApp().Start(rawConfig: rawConfig, workingDirectory: workingDirectory);

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
        var workingDirectory = Directory.GetCurrentDirectory();
        var rawConfig = new GenericStartup().GetDefaultConfig(workingDirectory);
        var result = new GenericStartApp().Start(
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
        var workingDirectory = Directory.GetCurrentDirectory();
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
        var app = new GenericStartApp();

        var result = app.Start(workingDirectory: Path.GetTempPath(), options: new StartupOptions
        {
            ConfigureBuilder = builder => builder.Services.AddSingleton<ITestRegistration>(new TestRegistration())
        });

        Assert.NotNull(result.Services);
        Assert.IsType<ConfigGeneric>(result.Config);
        Assert.IsType<AppServiceProvider>(result.ServiceProvider);
        Assert.NotNull(result.Services.GetRequiredService<ITestRegistration>());
    }

    [Fact]
    public void SqlServerStartApp_ExposesServicesAndTypedConfig()
    {
        var app = new SqlServerStartApp();

        var result = app.Start(workingDirectory: Path.GetTempPath(), options: new StartupOptions
        {
            ConfigureBuilder = builder => builder.Services.AddSingleton<ITestRegistration>(new TestRegistration())
        });

        Assert.NotNull(result.Services);
        Assert.IsType<MsConfig>(result.Config);
        Assert.IsType<AppServiceProvider>(result.ServiceProvider);
        Assert.NotNull(result.Services.GetRequiredService<ITestRegistration>());
    }

    [Fact]
    public void MySqlStartApp_ExposesServicesAndTypedConfig()
    {
        var app = new MySqlStartApp();

        var result = app.Start(workingDirectory: Path.GetTempPath(), options: new StartupOptions
        {
            ConfigureBuilder = builder => builder.Services.AddSingleton<ITestRegistration>(new TestRegistration())
        });

        Assert.NotNull(result.Services);
        Assert.IsType<MyConfig>(result.Config);
        Assert.IsType<MyAppServiceProvider>(result.ServiceProvider);
        Assert.NotNull(result.Services.GetRequiredService<ITestRegistration>());
    }

    [Fact]
    public void MariaDbStartApp_ExposesServicesAndTypedConfig()
    {
        var app = new MariaDbStartApp();

        var result = app.Start(workingDirectory: Path.GetTempPath(), options: new StartupOptions
        {
            ConfigureBuilder = builder => builder.Services.AddSingleton<ITestRegistration>(new TestRegistration())
        });

        Assert.NotNull(result.Services);
        Assert.IsType<MyConfig>(result.Config);
        Assert.IsType<MyAppServiceProvider>(result.ServiceProvider);
        Assert.NotNull(result.Services.GetRequiredService<ITestRegistration>());
    }

    private interface ITestRegistration;

    private sealed class TestRegistration : ITestRegistration;
}
