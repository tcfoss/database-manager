using System.Globalization;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.IntegrationTests;
using TcfOss.DatabaseManager.Core.Tests;
using MsSqlCommonHelpers = TcfOss.DatabaseManager.MsSql.IntegrationTests.CommonHelpers;

namespace TcfOss.DatabaseManager.App.IntegrationTests.MsSql;

public abstract class CliReadOnlyTests_MsSql<TFixture>
    : IClassFixture<TFixture>
    where TFixture : CliReadOnlyFixture_MsSql
{
    protected ITestOutputHelper TestOutputHelper { get; init; } = null!;


    [Fact]
    public async Task ParseFiles_NoOptions()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(Path.Combine(MsSqlCommonHelpers.GetSchemaDirectory("SimpleFilesForParsing").FullName));

        var cli = new CommandLineInterface();
        var parseResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "--dialect",
            nameof(SqlDialect.MsSql),
            "parse-files",
            "select.sql",
            "update.sql",
            "create_function.sql"
        ]);

        await CliReadOnlyTests_MsSql<TFixture>.TestParseFilesCommon(parseResult, fileFixture.RootDirectory.FullName, false, false, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ParseFiles_WithMeta()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(Path.Combine(MsSqlCommonHelpers.GetSchemaDirectory("SimpleFilesForParsing").FullName));

        var cli = new CommandLineInterface();
        var parseResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "--dialect",
            nameof(SqlDialect.MsSql),
            "parse-files",
            "select.sql",
            "update.sql",
            "create_function.sql",
            "--include-meta"
        ]);

        await CliReadOnlyTests_MsSql<TFixture>.TestParseFilesCommon(parseResult, fileFixture.RootDirectory.FullName, true, false, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ParseFiles_DuplicateSkip()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(Path.Combine(MsSqlCommonHelpers.GetSchemaDirectory("SimpleFilesForParsing").FullName));
        await File.WriteAllTextAsync(Path.Combine(fileFixture.RootDirectory.FullName, "select.parsed.json"), "Existing content", TestContext.Current.CancellationToken);

        var cli = new CommandLineInterface();
        var parseResult1 = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "--dialect",
            nameof(SqlDialect.MsSql),
            "parse-files",
            "select.sql",
            "update.sql",
            "create_function.sql",
        ]);

        await CliReadOnlyTests_MsSql<TFixture>.TestParseFilesCommon(parseResult1, fileFixture.RootDirectory.FullName, false, true, TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(fileFixture.RootDirectory.FullName, "select.parsed.json")));
        var selectText = await File.ReadAllTextAsync(Path.Combine(fileFixture.RootDirectory.FullName, "select.parsed.json"), TestContext.Current.CancellationToken);
        Assert.Equal("Existing content", selectText);
    }

    [Fact]
    public async Task ParseFiles_DuplicateError()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(Path.Combine(MsSqlCommonHelpers.GetSchemaDirectory("SimpleFilesForParsing").FullName));
        await File.WriteAllTextAsync(Path.Combine(fileFixture.RootDirectory.FullName, "select.parsed.json"), "Existing content", TestContext.Current.CancellationToken);

        var cli = new CommandLineInterface();
        var parseResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "--dialect",
            nameof(SqlDialect.MsSql),
            "parse-files",
            "select.sql",
            "update.sql",
            "create_function.sql",
            "--file-exists-action",
            "error"
        ]);

        Assert.NotEqual(0, parseResult);

        var actual = GetOutputLines(TestOutputHelper);
        var expected = string.Format(CultureInfo.CurrentCulture, MessageTemplates.FileExistsTemplate, Path.Combine(fileFixture.RootDirectory.FullName, "select.parsed.json"));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task ParseFiles_DuplicateOverwrite()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(Path.Combine(MsSqlCommonHelpers.GetSchemaDirectory("SimpleFilesForParsing").FullName));
        await File.WriteAllTextAsync(Path.Combine(fileFixture.RootDirectory.FullName, "select.parsed.json"), "Existing content", TestContext.Current.CancellationToken);

        var cli = new CommandLineInterface();
        var parseResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "--dialect",
            nameof(SqlDialect.MsSql),
            "parse-files",
            "select.sql",
            "update.sql",
            "create_function.sql",
            "--file-exists-action",
            "overwrite"
        ]);

        await CliReadOnlyTests_MsSql<TFixture>.TestParseFilesCommon(parseResult, fileFixture.RootDirectory.FullName, false, false, TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(fileFixture.RootDirectory.FullName, "select.parsed.json")));
        var selectText = await File.ReadAllTextAsync(Path.Combine(fileFixture.RootDirectory.FullName, "select.parsed.json"), TestContext.Current.CancellationToken);
        Assert.DoesNotContain("Existing content", selectText);
    }

    [Fact]
    public async Task ParseFiles_DuplicateRename()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(Path.Combine(MsSqlCommonHelpers.GetSchemaDirectory("SimpleFilesForParsing").FullName));
        await File.WriteAllTextAsync(Path.Combine(fileFixture.RootDirectory.FullName, "select.parsed.json"), "Existing content", TestContext.Current.CancellationToken);

        var cli = new CommandLineInterface();
        var parseResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "--dialect",
            nameof(SqlDialect.MsSql),
            "parse-files",
            "select.sql",
            "update.sql",
            "create_function.sql",
            "--file-exists-action",
            "rename"
        ]);

        await CliReadOnlyTests_MsSql<TFixture>.TestParseFilesCommon(parseResult, fileFixture.RootDirectory.FullName, false, false, TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(fileFixture.RootDirectory.FullName, "select.parsed.json.bak.1")));
        var selectText = await File.ReadAllTextAsync(Path.Combine(fileFixture.RootDirectory.FullName, "select.parsed.json.bak.1"), TestContext.Current.CancellationToken);
        Assert.Contains("Existing content", selectText);
    }

    [Fact]
    public void ParseFiles_NonExistentFile()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(Path.Combine(MsSqlCommonHelpers.GetSchemaDirectory("SimpleFilesForParsing").FullName));

        var cli = new CommandLineInterface();
        var parseResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "--dialect",
            nameof(SqlDialect.MsSql),
            "parse-files",
            "non_existent.sql",
            "update.sql",
            "create_function.sql"
        ]);

        Assert.NotEqual(0, parseResult);

        var actual = GetOutputLines(TestOutputHelper);
        var expected = $"File not found: {Path.Combine(fileFixture.RootDirectory.FullName, "non_existent.sql")}";
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task ParseFiles_GlobPattern()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(Path.Combine(MsSqlCommonHelpers.GetSchemaDirectory("SimpleFilesForParsing").FullName));
        await File.WriteAllTextAsync(fileFixture.CombinePath("extra_a.sql"), "SELECT 1;", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(fileFixture.CombinePath("extra_b.sql"), "SELECT 2;", TestContext.Current.CancellationToken);

        var cli = new CommandLineInterface();
        var parseResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "--dialect",
            nameof(SqlDialect.MsSql),
            "parse-files",
            "select.sql",
            "update.sql",
            "create_function.sql",
            "extra_*.sql"
        ]);

        await CliReadOnlyTests_MsSql<TFixture>.TestParseFilesCommon(parseResult, fileFixture.RootDirectory.FullName, false, false, TestContext.Current.CancellationToken);
        Assert.True(File.Exists(fileFixture.CombinePath("extra_a.parsed.json")), "Parsed extra_a file was not created.");
        Assert.True(File.Exists(fileFixture.CombinePath("extra_b.parsed.json")), "Parsed extra_b file was not created.");
    }

    [Fact]
    public void ParseErrorFakeJoin()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFile(Path.Combine(MsSqlCommonHelpers.GetSchemaDirectory("FilesWithErrors").FullName, "parse_error_fake_join.sql"));

        var cli = new CommandLineInterface();
        var parseResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "--dialect",
            nameof(SqlDialect.MsSql),
            "parse-files",
            "parse_error_fake_join.sql",
        ]);

        Assert.NotEqual(0, parseResult);

        var actual = GetOutputLines(TestOutputHelper);
        var filePath = Path.Combine(fileFixture.RootDirectory.FullName, "parse_error_fake_join.sql");
        var expected = $"""
            Parse Error: Expected end of statement. Found FAKEJOIN. (In file '{filePath}' at line 3, column 1)
            FAKEJOIN othertable AS ot
            ^
            """;
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ParseErrorNotDroppable()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFile(Path.Combine(MsSqlCommonHelpers.GetSchemaDirectory("FilesWithErrors").FullName, "parse_error_not_droppable.sql"));

        var cli = new CommandLineInterface();
        var parseResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "--dialect",
            nameof(SqlDialect.MsSql),
            "parse-files",
            "parse_error_not_droppable.sql",
        ]);

        Assert.NotEqual(0, parseResult);

        var actual = GetOutputLines(TestOutputHelper);
        var filePath = Path.Combine(fileFixture.RootDirectory.FullName, "parse_error_not_droppable.sql");
        var expected = $$"""
            Parse Error: Expected one of { TABLE | PROCEDURE | FUNCTION | TRIGGER | VIEW | EVENT | SCHEMA | DATABASE | INDEX }. Found CARROT. (In file '{{filePath}}' at line 1, column 6)
            DROP CARROT cant_drop;
                 ^
            """;
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void LexErrorUnclosedQuote()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFile(Path.Combine(MsSqlCommonHelpers.GetSchemaDirectory("FilesWithErrors").FullName, "lex_error_unclosed_quote.sql"));

        var cli = new CommandLineInterface();
        var parseResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "--dialect",
            nameof(SqlDialect.MsSql),
            "parse-files",
            "lex_error_unclosed_quote.sql",
        ]);

        Assert.NotEqual(0, parseResult);

        var actual = GetOutputLines(TestOutputHelper);
        var filePath = Path.Combine(fileFixture.RootDirectory.FullName, "lex_error_unclosed_quote.sql");
        var expected = $"""
            Lexing Error: Unterminated string literal. (In file '{filePath}' at line 1, column 9)
            DECLARE 'myvar
                    ^
            """;
        Assert.Equal(expected, actual);
    }

    // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
    private static async Task TestParseFilesCommon(int parseResult, string activeDirectory, bool includeMeta, bool skipSelect, CancellationToken cancellationToken = default)
    {
        Assert.Equal(0, parseResult);

        if (!skipSelect)
        {
            var selectPath = Path.Combine(activeDirectory, "select.parsed.json");
            Assert.True(File.Exists(selectPath), "Parsed select file was not created.");
            var selectText = await File.ReadAllTextAsync(selectPath, cancellationToken);
            Assert.Contains("TcfOss.DatabaseManager.Core.Statements.Select", selectText);
            var metaPattern = @"""Body"": "" line comment\\n"",\s+""Prefix"": ""--""";
            if (includeMeta)
            {
                Assert.Matches(metaPattern, selectText);
            }
            else
            {
                Assert.DoesNotMatch(metaPattern, selectText);
            }
        }

        var updatePath = Path.Combine(activeDirectory, "update.parsed.json");
        Assert.True(File.Exists(updatePath), "Parsed update file was not created.");
        var updateText = await File.ReadAllTextAsync(updatePath, cancellationToken);
        Assert.Contains("TcfOss.DatabaseManager.Core.Statements.Update", updateText);

        var functionPath = Path.Combine(activeDirectory, "create_function.parsed.json");
        Assert.True(File.Exists(functionPath), "Parsed function file was not created.");
        var functionText = await File.ReadAllTextAsync(functionPath, cancellationToken);
        Assert.Contains("TcfOss.DatabaseManager.Core.Statements.CreateFunction", functionText);

        if (includeMeta)
        {
            Assert.Contains("\"Meta\":", functionText);
        }
        else
        {
            Assert.DoesNotContain("\"Meta\":", functionText);
        }
    }

    private static string GetOutputLines(ITestOutputHelper outputHelper)
    {
        var lines = outputHelper.Output.Split(Environment.NewLine);
        for (var i = lines.Length - 1; i >= 0; i--)
        {
            if (lines[i].StartsWith("[testcontainers"))
            {
                return string.Join(Environment.NewLine, lines.Skip(i + 1)).Trim();
            }
        }
        return string.Join(Environment.NewLine, lines).Trim();
    }
}
