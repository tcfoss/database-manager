
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
        var schemaDirectory = Path.Combine(fixture.RootDirectory.FullName, "MySchema");
        Directory.CreateDirectory(schemaDirectory);

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
        var schemaDirectory = Path.Combine(fixture.RootDirectory.FullName, "MySchema");
        Directory.CreateDirectory(schemaDirectory);

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
        var schemaDirectory = Path.Combine(fixture.RootDirectory.FullName, "MySchema");
        Directory.CreateDirectory(schemaDirectory);

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
        var schemaDirectory = Path.Combine(fixture.RootDirectory.FullName, "MySchema");
        Directory.CreateDirectory(schemaDirectory);

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
        var schemaDirectory = Path.Combine(fixture.RootDirectory.FullName, "MySchema");
        Directory.CreateDirectory(schemaDirectory);

        await PrepareFixture(fixture, refactorContents);

        var ex = Assert.Throws<ConfigurationException.MissingFieldException>(() => BuildConfig(fixture.RootDirectory.FullName));
        Assert.Contains("TableName", ex.Message);
    }

    private static async Task PrepareFixture(FsProjectFixture fixture, string refactorContents)
    {
        var schemaDirectory = Path.Combine(fixture.RootDirectory.FullName, "MySchema");
        Directory.CreateDirectory(schemaDirectory);

        await fixture.WriteTextAsync(Path.Combine(schemaDirectory, "refactor.yaml"), refactorContents, TestContext.Current.CancellationToken);
    }

    private static GenericConfig BuildConfig(string rootDirectory)
    {
        var builder = new GenericConfigurationBuilder();
        var rawConfig = new ConfigParsing.Config()
        {
            ProjectDirectory = rootDirectory,
            Dialect = SqlDialect.Generic,
            QuoteStyle = QuoteStyle.Ansi,
            Schemas = [
                new ConfigParsing.SchemaMapping()
                {
                    SchemaName = "my_schema",
                    RootPath = "MySchema",
                    RefactorFiles = ["refactor.yaml"]
                }
            ]
        };
        return builder.BuildConfiguration(rootDirectory, rawConfig, new EnvironmentVariableReader(), false, NullLogger.Instance);
    }
}
