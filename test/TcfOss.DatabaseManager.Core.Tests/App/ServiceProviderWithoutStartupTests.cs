using TcfOss.DatabaseManager.Core.App;

namespace TcfOss.DatabaseManager.Core.Tests.App;

public class ServiceProviderWithoutStartupTests
{
    [Fact]
    public void AllMethodsThrow()
    {
        Assert.Throws<InvalidOperationException>(() => AppServiceProvider.SchemaDownloader);
        Assert.Throws<InvalidOperationException>(() => AppServiceProvider.ChangeComputer);
        Assert.Throws<InvalidOperationException>(() => AppServiceProvider.RefactorLoader);
        Assert.Throws<InvalidOperationException>(() => AppServiceProvider.ScopeFactory);
        Assert.Throws<InvalidOperationException>(AppServiceProvider.GetLogger<object>);
    }
}
