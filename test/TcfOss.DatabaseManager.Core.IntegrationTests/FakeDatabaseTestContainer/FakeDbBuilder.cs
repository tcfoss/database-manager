using Docker.DotNet.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;

namespace TcfOss.DatabaseManager.Core.IntegrationTests.FakeDatabaseTestContainer;

public class FakeDbBuilder : ContainerBuilder<FakeDbBuilder, FakeDbContainer, FakeDbConfiguration>
{
    public const ushort FakeDbPort = 1234;
    private const string DefaultDatabase = "fakedb";
    private const string DefaultUsername = "fakeuser";
    private const string DefaultPassword = "fakepassword";


    protected override FakeDbConfiguration DockerResourceConfiguration { get; }

    private FakeDbBuilder(FakeDbConfiguration resourceConfiguration)
        : base(resourceConfiguration)
    {
        DockerResourceConfiguration = resourceConfiguration;
    }

    public FakeDbBuilder()
        : this(new FakeDbConfiguration())
    {
    }
    public override FakeDbContainer Build()
    {
        return new FakeDbContainer(DockerResourceConfiguration);
    }

    protected override FakeDbBuilder Clone(IContainerConfiguration resourceConfiguration)
    {
        return Merge(DockerResourceConfiguration, new FakeDbConfiguration(resourceConfiguration));
    }

    protected override FakeDbBuilder Clone(IResourceConfiguration<CreateContainerParameters> resourceConfiguration)
    {
        return Merge(DockerResourceConfiguration, new FakeDbConfiguration(resourceConfiguration));
    }

    protected override FakeDbBuilder Merge(FakeDbConfiguration oldValue, FakeDbConfiguration newValue)
    {
        return new FakeDbBuilder(new FakeDbConfiguration(oldValue, newValue));
    }

    private FakeDbBuilder WithDatabase(string database)
    {
        return Merge(DockerResourceConfiguration, new FakeDbConfiguration(database: database))
            .WithEnvironment("FAKEDB_DATABASE", database);
    }

    private FakeDbBuilder WithUsername(string username)
    {
        return Merge(DockerResourceConfiguration, new FakeDbConfiguration(username: username))
            .WithEnvironment("FAKEDB_USERNAME", "root".Equals(username, StringComparison.OrdinalIgnoreCase) ? "" : username);
    }

    private FakeDbBuilder WithPassword(string password)
    {
        return Merge(DockerResourceConfiguration, new FakeDbConfiguration(password: password))
            .WithEnvironment("FAKEDB_PASSWORD", password)
            .WithEnvironment("FAKEDB_ROOT_PASSWORD", password);
    }

    protected override FakeDbBuilder Init()
    {
        return base.Init()
            .WithPortBinding(FakeDbPort, true)
            .WithDatabase(DefaultDatabase)
            .WithUsername(DefaultUsername)
            .WithPassword(DefaultPassword);
    }
}
