using TcfOss.DatabaseManager.Core.IntegrationTests.FakeDatabaseTestContainer;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.MsSql.IntegrationTests.DatabaseFixtures;

public sealed class FakeDbFixture(IMessageSink messageSink)
    : DbFixture<FakeDbBuilder, FakeDbContainer>(messageSink)
{
    protected override FakeDbBuilder Configure()
    {
        return new FakeDbBuilder();
    }
}
