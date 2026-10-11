using TcfOss.DatabaseManager.Core.Hosting;

namespace TcfOss.DatabaseManager.Hosting.Tests;

public class ServiceProviderWithoutStartupTests
{
    [Fact]
    public void AllMethodsThrow()
    {
        AppServiceProvider.ServiceProvider = null!;
        Assert.Throws<InvalidOperationException>(() => AppServiceProvider.SchemaDownloader);
        Assert.Throws<InvalidOperationException>(() => AppServiceProvider.ChangeComputer);
        Assert.Throws<InvalidOperationException>(() => AppServiceProvider.RefactorLoader);
        Assert.Throws<InvalidOperationException>(AppServiceProvider.GetLogger<object>);
    }
}
