using Microsoft.Extensions.DependencyInjection;
using TcfOss.DatabaseManager.Core.Hosting;
using TcfOss.DatabaseManager.Core.IntegrationTests;
using TcfOss.DatabaseManager.MsSql.Configuration;
using TcfOss.DatabaseManager.MsSql.Hosting;

namespace TcfOss.DatabaseManager.MsSql.IntegrationTests.Hosting;

public class SqlServerStartAppIntegrationTests
{
    [Fact]
    public void SqlServerStartApp_UsesFallbackConfigurationWhenServerIsUnavailable()
    {
        using var fixture = new FsProjectFixture();
        var rawConfig = new MsConfigurationBuilder().GetDefaultConfig(fixture.RootDirectory.FullName);
        rawConfig.Credentials!.Hostname = "127.0.0.1";
        rawConfig.Credentials.Port = "1";
        rawConfig.Credentials.ConnectionTimeout = 1;

        using var result = new SqlServerStartApp().Start(
            rawConfig: rawConfig,
            workingDirectory: fixture.RootDirectory.FullName,
            options: new StartupOptions
            {
                ConfigureBuilder = builder => builder.Services.AddSingleton<ITestRegistration>(new TestRegistration())
            });

        Assert.NotNull(result.Services);
        Assert.IsType<MsConfig>(result.Config);
        Assert.IsType<AppServiceProvider>(result.ServiceProvider);
        Assert.NotNull(result.Services.GetRequiredService<ITestRegistration>());
        Assert.False(result.Config.DatabaseAvailable);
    }

    private interface ITestRegistration;

    private sealed class TestRegistration : ITestRegistration;
}
