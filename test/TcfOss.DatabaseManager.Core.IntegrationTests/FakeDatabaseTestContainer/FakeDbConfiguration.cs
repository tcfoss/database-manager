using Docker.DotNet.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;

namespace TcfOss.DatabaseManager.Core.IntegrationTests.FakeDatabaseTestContainer;

#pragma warning disable

public class FakeDbConfiguration : ContainerConfiguration
{
    public string Database { get; }
    public string Username { get; }
    public string Password { get; }

    public FakeDbConfiguration(string database = null, string username = null, string password = null)
    {
        Database = database;
        Username = username;
        Password = password;
    }

    public FakeDbConfiguration(IContainerConfiguration resourceConfiguration)
        : base(resourceConfiguration)
    {
    }

    public FakeDbConfiguration(FakeDbConfiguration oldValue, FakeDbConfiguration newValue)
        : base(oldValue, newValue)
    {
        Database = BuildConfiguration.Combine(oldValue.Database, newValue.Database);
        Username = BuildConfiguration.Combine(oldValue.Username, newValue.Username);
        Password = BuildConfiguration.Combine(oldValue.Password, newValue.Password);
    }

    public FakeDbConfiguration(IResourceConfiguration<CreateContainerParameters> resourceConfiguration)
        : base(resourceConfiguration)
    {
    }
}

#pragma warning restore