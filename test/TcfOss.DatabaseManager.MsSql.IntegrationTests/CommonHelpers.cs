using DotNet.Testcontainers.Builders;
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
}
