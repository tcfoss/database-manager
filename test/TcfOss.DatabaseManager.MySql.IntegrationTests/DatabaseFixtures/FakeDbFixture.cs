using TcfOss.DatabaseManager.Core.IntegrationTests.FakeDatabaseTestContainer;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.MySql.IntegrationTests.DatabaseFixtures;

public sealed class FakeDbFixture(IMessageSink messageSink)
    : DbFixture<FakeDbBuilder, FakeDbContainer>(messageSink)
{
    protected override ushort Port => 3306;

    protected override FakeDbBuilder Configure()
    {
        return new FakeDbBuilder();
    }
}
