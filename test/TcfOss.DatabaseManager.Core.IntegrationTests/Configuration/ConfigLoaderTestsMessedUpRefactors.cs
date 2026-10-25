
using Microsoft.Extensions.Logging.Abstractions;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.Errors;
using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.Core.IntegrationTests.Configuration;

public class ConfigLoaderTestsMessedUpRefactors
{
    [Fact]
    public async Task TableRefactor_MissingOldName_Throws()
    {
        using var fixture = new FsProjectFixture();

        var refactorContents = """
        - UniqueId: abc123
          Type: TableRename
          NewName: new_table_name
        """;
        await PrepareFixture(fixture, refactorContents);

        var ex = Assert.Throws<ConfigurationException.MissingFieldException>(() => BuildConfig(fixture.RootDirectory.FullName));
        Assert.Contains("OldName", ex.Message);
    }

    [Fact]
    public async Task TableRefactor_MissingNewName_Throws()
    {
        using var fixture = new FsProjectFixture();

        var refactorContents = """
        - UniqueId: abc123
          Type: TableRename
          OldName: old_table_name
        """;
        await PrepareFixture(fixture, refactorContents);

        var ex = Assert.Throws<ConfigurationException.MissingFieldException>(() => BuildConfig(fixture.RootDirectory.FullName));
        Assert.Contains("NewName", ex.Message);
    }

    [Fact]
    public async Task ColumnRefactor_MissingOldName_Throws()
    {
        using var fixture = new FsProjectFixture();

        var refactorContents = """
        - UniqueId: abc123
          Type: ColumnRename
          TableName: my_table
          NewName: new_column_name
        """;
        await PrepareFixture(fixture, refactorContents);

        var ex = Assert.Throws<ConfigurationException.MissingFieldException>(() => BuildConfig(fixture.RootDirectory.FullName));
        Assert.Contains("OldName", ex.Message);
    }

    [Fact]
    public async Task ColumnRefactor_MissingNewName_Throws()
    {
        using var fixture = new FsProjectFixture();

        var refactorContents = """
        - UniqueId: abc123
          Type: ColumnRename
          TableName: my_table
          OldName: old_column_name
        """;
        await PrepareFixture(fixture, refactorContents);

        var ex = Assert.Throws<ConfigurationException.MissingFieldException>(() => BuildConfig(fixture.RootDirectory.FullName));
        Assert.Contains("NewName", ex.Message);
    }

    [Fact]
    public async Task ColumnRefactor_MissingTableName_Throws()
    {
        using var fixture = new FsProjectFixture();

        var refactorContents = """
        - UniqueId: abc123
          Type: ColumnRename
          OldName: old_column_name
          NewName: new_column_name
        """;
        await PrepareFixture(fixture, refactorContents);

        var ex = Assert.Throws<ConfigurationException.MissingFieldException>(() => BuildConfig(fixture.RootDirectory.FullName));
        Assert.Contains("TableName", ex.Message);
    }

    [Fact]
    public async Task Refactors_DuplicateUniqueId_Throws()
    {
        using var fixture = new FsProjectFixture();

        var refactorContents = """
        - UniqueId: abc123
          Type: TableRename
          OldName: old_table_name
          NewName: new_table_name
        - UniqueId: abc123
          Type: TableRename
          OldName: other_old_name
          NewName: other_new_name
        """;

        await PrepareFixture(fixture, refactorContents);

        var ex = Assert.Throws<ConfigurationException.DuplicateRefactorId>(() => BuildConfig(fixture.RootDirectory.FullName));
        Assert.Contains("abc123", ex.Message);
    }

    [Fact]
    public async Task Refactors_DuplicateUniqueIdAcrossSchemas_Throws()
    {
        using var fixture = new FsProjectFixture();

        var refactorContents = """
        - UniqueId: abc123
          Type: TableRename
          OldName: old_table_name
          NewName: new_table_name
        """;

        foreach (var directoryName in new[] { "SchemaA", "SchemaB" })
        {
            await fixture.WriteTextAsync(Path.Combine(directoryName, "refactor.yaml"), refactorContents, TestContext.Current.CancellationToken);
        }

        var schemas = new[]
        {
            new ConfigParsing.SchemaMapping { SchemaName = "schema_a", RootPath = "SchemaA", RefactorFiles = ["refactor.yaml"] },
            new ConfigParsing.SchemaMapping { SchemaName = "schema_b", RootPath = "SchemaB", RefactorFiles = ["refactor.yaml"] },
        };

        var ex = Assert.Throws<ConfigurationException.DuplicateRefactorId>(() => BuildConfig(fixture.RootDirectory.FullName, schemas));
        Assert.Contains("abc123", ex.Message);
    }

    private static async Task PrepareFixture(FsProjectFixture fixture, string refactorContents)
    {
        await fixture.WriteTextAsync(Path.Combine("MySchema", "refactor.yaml"), refactorContents, TestContext.Current.CancellationToken);
    }

    private static GenericConfig BuildConfig(string rootDirectory)
    {
        return BuildConfig(rootDirectory,
        [
            new ConfigParsing.SchemaMapping()
            {
                SchemaName = "my_schema",
                RootPath = "MySchema",
                RefactorFiles = ["refactor.yaml"]
            }
        ]);
    }

    private static GenericConfig BuildConfig(string rootDirectory, ConfigParsing.SchemaMapping[] schemas)
    {
        var builder = new GenericConfigurationBuilder();
        var rawConfig = new ConfigParsing.Config()
        {
            ProjectDirectory = rootDirectory,
            Dialect = SqlDialect.Generic,
            QuoteStyle = QuoteStyle.Ansi,
            Schemas = schemas
        };
        return builder.BuildConfiguration(rootDirectory, rawConfig, new EnvironmentVariableReader(), false, NullLogger.Instance);
    }
}
