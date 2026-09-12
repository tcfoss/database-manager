using Microsoft.Extensions.DependencyInjection;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.LibWrapper;
using TcfOss.DatabaseManager.MariaDb.LibWrapper;
using TcfOss.DatabaseManager.MsSql.Configuration;
using TcfOss.DatabaseManager.MsSql.LibWrapper;
using TcfOss.DatabaseManager.MySql.App;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.LibWrapper;

namespace TcfOss.DatabaseManager.LibWrapper.Tests;

public class ProviderSpecificStartAppTests
{
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
