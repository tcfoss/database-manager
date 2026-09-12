using TcfOss.DatabaseManager.Core.IntegrationTests.FakeDatabaseTestContainer;
using TcfOss.DatabaseManager.MySql.IntegrationTests.DatabaseFixtures;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.App.IntegrationTests.MySqlFamily;

// ReSharper disable ClassNeverInstantiated.Global
public class CliReadOnlyFixture_NoDatabase
    : CliReadOnlyFixture<FakeDbBuilder, FakeDbContainer>
{
    public CliReadOnlyFixture_NoDatabase(IMessageSink messageSink)
    {
        DbFixture = new FakeDbFixture(messageSink);
    }
}
