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

namespace TcfOss.DatabaseManager.Core.Tests.App;

public sealed class SqlFormatterTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("dbman-format-tests-");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FormatSql_InPlace_FormatsEveryFileAndHonorsBackupSetting(bool noBackup)
    {
        string firstPath = Path.Combine(_directory.FullName, "first.sql");
        string secondPath = Path.Combine(_directory.FullName, "second.sql");
        File.WriteAllText(firstPath, "select 1");
        File.WriteAllText(secondPath, "select 2");

        var formatter = CreateFormatter();
        formatter.FormatSql([firstPath, secondPath], null, noBackup: noBackup);

        Assert.Equal($"SELECT{Environment.NewLine}    1;", File.ReadAllText(firstPath));
        Assert.Equal($"SELECT{Environment.NewLine}    2;", File.ReadAllText(secondPath));
        if (noBackup)
        {
            Assert.Empty(Directory.GetFiles(_directory.FullName, "*.bak.*"));
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
        string inputPath = Path.Combine(_directory.FullName, "input.sql");
        string outputPath = Path.Combine(_directory.FullName, "formatted-input.sql");
        File.WriteAllText(inputPath, "select 1");
        File.WriteAllText(outputPath, "existing output");
        var formatter = CreateFormatter();

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

    private SqlFormatter<ConfigBase> CreateFormatter()
    {
        var config = new ConfigBase
        {
            ProjectDirectory = _directory.FullName,
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

    public void Dispose()
    {
        _directory.Delete(recursive: true);
    }
}
