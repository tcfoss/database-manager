using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using TcfOss.DatabaseManager.Core.IntegrationTests.FakeDatabaseTestContainer;
using Testcontainers.MsSql;

namespace TcfOss.DatabaseManager.MsSql.IntegrationTests;

public static class CommonHelpers
{
    private static readonly DirectoryInfo s_schemasDirectory = new(Path.Combine(CommonDirectoryPath.GetProjectDirectory().DirectoryPath, "..", "Resources", "TestSchemas", "MsSql"));

    public static DirectoryInfo GetSchemaDirectory(string testSchemaName)
    {
        return new DirectoryInfo(Path.Combine(s_schemasDirectory.FullName, testSchemaName));
    }

    public static MsSqlBuilder WithStandardOptions(this MsSqlBuilder builder)
    {
        return builder
            .WithName($"container_mssql_{Guid.NewGuid():N}")
            .WithPassword("yourStrongPassword123");
    }

    public static ushort GetPort(this IDatabaseContainer container)
    {
        return container switch
        {
            MsSqlContainer => MsSqlBuilder.MsSqlPort,
            FakeDbContainer => FakeDbBuilder.FakeDbPort,
            _ => throw new ArgumentException($"Container type {container.GetType().Name} is not supported.")
        };
    }

    public static ushort GetMappedPort(this IDatabaseContainer container)
    {
        var internalPort = container.GetPort();
        return container.GetMappedPublicPort(internalPort);
    }

    public static void WriteBasicConfig(this IDatabaseContainer container, DirectoryInfo directory)
    {
        var mappedPort = container.GetMappedPort();

        var contents = $"""
        ProjectDirectory: {directory.FullName}
        Dialect: MsSql
        QuoteStyle: Brackets
        Catalog: testcat

        Credentials:
          HostName: localhost
          Username: sa
          Password: yourStrongPassword123
          Port: {mappedPort}
          TrustServerCertificate: true
          ConnectionTimeout: 1

        Schemas:
          - SchemaName: testschema
            RootPath: TestSchema
        """;
        File.WriteAllText(Path.Combine(directory.FullName, "database-manager.yaml"), contents);
    }
}
