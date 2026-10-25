using Microsoft.Extensions.Logging.Abstractions;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Parsing;

namespace TcfOss.DatabaseManager.Core.IntegrationTests.App;

public sealed class SqlFormatterTests(FsProjectFixture fixture) : IClassFixture<FsProjectFixture>
{
    private FsProjectFixture Fixture { get; } = fixture;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FormatSql_InPlace_FormatsEveryFileAndHonorsBackupSetting(bool noBackup)
    {
        string directory = CreateTestDirectory();
        string firstPath = Path.Combine(directory, "first.sql");
        string secondPath = Path.Combine(directory, "second.sql");
        File.WriteAllText(firstPath, "select 1");
        File.WriteAllText(secondPath, "select 2");

        var formatter = CreateFormatter(directory);
        formatter.FormatSql([firstPath, secondPath], null, noBackup: noBackup);

        Assert.Equal($"SELECT{Environment.NewLine}    1;", File.ReadAllText(firstPath));
        Assert.Equal($"SELECT{Environment.NewLine}    2;", File.ReadAllText(secondPath));
        if (noBackup)
        {
            Assert.Empty(Directory.GetFiles(directory, "*.bak.*"));
        }
        else
        {
            Assert.Equal("select 1", File.ReadAllText(firstPath + ".bak.1"));
            Assert.Equal("select 2", File.ReadAllText(secondPath + ".bak.1"));
        }
    }

    [Theory]
    [InlineData(FileExistsAction.Overwrite)]
    [InlineData(FileExistsAction.Skip)]
    [InlineData(FileExistsAction.Rename)]
    [InlineData(FileExistsAction.Error)]
    public void FormatSql_OutputPattern_PreservesInputAndHonorsExistingFilePolicy(FileExistsAction action)
    {
        string directory = CreateTestDirectory();
        string inputPath = Path.Combine(directory, "input.sql");
        string outputPath = Path.Combine(directory, "formatted-input.sql");
        File.WriteAllText(inputPath, "select 1");
        File.WriteAllText(outputPath, "existing output");
        var formatter = CreateFormatter(directory);

        if (action == FileExistsAction.Error)
        {
            Assert.Throws<CommandException.FileExists>(() => formatter.FormatSql([inputPath], "formatted-{fileName}.sql", fileExistsAction: action));
        }
        else
        {
            formatter.FormatSql([inputPath], "formatted-{fileName}.sql", fileExistsAction: action);
        }

        Assert.Equal("select 1", File.ReadAllText(inputPath));
        Assert.False(File.Exists(inputPath + ".bak.1"));
        string expected = action is FileExistsAction.Skip or FileExistsAction.Error ? "existing output" : $"SELECT{Environment.NewLine}    1;";
        Assert.Equal(expected, File.ReadAllText(outputPath));
        if (action == FileExistsAction.Rename)
        {
            Assert.Equal("existing output", File.ReadAllText(outputPath + ".bak.1"));
        }
        else
        {
            Assert.False(File.Exists(outputPath + ".bak.1"));
        }
    }

    private string CreateTestDirectory()
    {
        string path = Fixture.CombinePath(Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static SqlFormatter<ConfigBase> CreateFormatter(string projectDirectory)
    {
        var config = new ConfigBase
        {
            ProjectDirectory = projectDirectory,
            Catalog = new CatalogIdentifier("def"),
            Dialect = SqlDialect.Generic,
            QuoteStyle = QuoteStyle.Ansi,
            ValidationSettings = new ValidationSettings(),
            DatabaseAvailable = false,
        };
        return new SqlFormatter<ConfigBase>(config,
            new FileWriter(NullLogger<FileWriter>.Instance),
            new TextParser(new Lexer<RunState>(), new Parser()),
            new FunctionNameProvider(),
            NullLogger<SqlFormatter<ConfigBase>>.Instance);
    }
}
