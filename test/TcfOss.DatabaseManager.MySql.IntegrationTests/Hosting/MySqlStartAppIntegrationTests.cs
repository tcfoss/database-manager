using Microsoft.Extensions.DependencyInjection;
using TcfOss.DatabaseManager.Core.Hosting;
using TcfOss.DatabaseManager.Core.IntegrationTests;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.Hosting;

namespace TcfOss.DatabaseManager.MySql.IntegrationTests.Hosting;

public class MySqlStartAppIntegrationTests
{
    [Fact]
    public void MySqlStartApp_UsesFallbackConfigurationWhenServerIsUnavailable()
    {
        using var fixture = new FsProjectFixture();
        var rawConfig = new MyConfigurationBuilder().GetDefaultConfig(fixture.RootDirectory.FullName);
        rawConfig.Credentials!.Hostname = "127.0.0.1";
        rawConfig.Credentials.Port = "1";
        rawConfig.Credentials.ConnectionTimeout = 1;
        rawConfig.Credentials.SocketPath = null;

        using var result = new MySqlStartApp().Start(
            rawConfig: rawConfig,
            workingDirectory: fixture.RootDirectory.FullName,
            options: new StartupOptions
            {
                ConfigureBuilder = builder => builder.Services.AddSingleton<ITestRegistration>(new TestRegistration())
            });

        Assert.NotNull(result.Services);
        Assert.IsType<MyConfig>(result.Config);
        Assert.IsType<MyAppServiceProvider>(result.ServiceProvider);
        Assert.NotNull(result.Services.GetRequiredService<ITestRegistration>());
        Assert.False(result.Config.DatabaseAvailable);
    }

    private interface ITestRegistration;

    private sealed class TestRegistration : ITestRegistration;
}
