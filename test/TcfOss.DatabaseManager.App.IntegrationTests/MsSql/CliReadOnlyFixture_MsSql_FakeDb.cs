using TcfOss.DatabaseManager.Core.IntegrationTests.FakeDatabaseTestContainer;
using TcfOss.DatabaseManager.MsSql.IntegrationTests.DatabaseFixtures;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.App.IntegrationTests.MsSql;

// ReSharper disable ClassNeverInstantiated.Global
public class CliReadOnlyFixture_MsSql_FakeDb : CliReadOnlyFixture_MsSql<FakeDbBuilder, FakeDbContainer>
{
    public CliReadOnlyFixture_MsSql_FakeDb(IMessageSink messageSink)
    {
        DbFixture = new FakeDbFixture(messageSink);
    }
}
