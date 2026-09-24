using System.Globalization;
using DotNet.Testcontainers.Containers;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.IntegrationTests;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Resources;
using TcfOss.DatabaseManager.Core.Tests;
using TcfOss.DatabaseManager.Core.Tests.Resources;
using TcfOss.DatabaseManager.MySql.BuiltIn;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;
using TcfOss.DatabaseManager.MySql.IntegrationTests;
using GenerationMode = TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes.GenerationMode;

namespace TcfOss.DatabaseManager.App.IntegrationTests.MySqlFamily;

public abstract class CliReadOnlyTests<TFixture> : IClassFixture<TFixture>
    where TFixture : CliReadOnlyFixture
{
    protected TFixture Fixture { get; init; } = null!;
    private IDatabaseContainer Container => Fixture.Container;
    protected ITestOutputHelper TestOutputHelper { get; init; } = null!;

    protected abstract SqlDialect Dialect { get; }

    protected virtual string DefaultCharset => "utf8mb4";
    protected virtual string DefaultCollation => "utf8mb4_0900_ai_ci";
    protected virtual string IfConditionBegin => "";
    protected virtual string IfConditionEnd => "";
    protected virtual string DefaultParameterDirection => "";
    protected virtual uint? DefaultIntWidth => null;
    private string DefaultIntWidthString => DefaultIntWidth != null ? $"({DefaultIntWidth})" : "";

    protected virtual bool ConnectionAvailable => true;


    [Fact]
    public void ComputeChanges_NoConfig_NoDialect_Fails()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(Path.Combine(CommonHelpers.GetSchemaDirectory("Initial").FullName));

        File.Delete(fileFixture.CombinePath("database-manager.yaml"));

        var cli = new CommandLineInterface();
        var computeResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "compute-changes",
            "changes.sql"
        ]);

        Assert.NotEqual(0, computeResult);

        var actual = GetOutputLines(TestOutputHelper);
        var expected = string.Format(CultureInfo.CurrentCulture, MessageTemplates.DialectRequiredTemplate, "compute-changes");
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ComputeChanges_NoConfig_WithDialect_Fails()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(Path.Combine(CommonHelpers.GetSchemaDirectory("Initial").FullName));

        File.Delete(fileFixture.CombinePath("database-manager.yaml"));

        var cli = new CommandLineInterface();
        var computeResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "--dialect",
            Dialect.ToString(),
            "compute-changes",
            "changes.sql"
        ]);

        Assert.NotEqual(0, computeResult);

        var actual = GetOutputLines(TestOutputHelper);
        var expected = string.Format(CultureInfo.CurrentCulture, MessageTemplates.ConfigRequiredTemplate, "compute-changes");
        Assert.Equal(expected, actual);
    }

    [Fact]
    public virtual async Task DownloadSchema()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFile(Path.Combine(CommonHelpers.GetSchemaDirectory("Initial").FullName, "database-manager.yaml"));

        UpdateConfiguration(fileFixture.RootDirectory.FullName);

        var cli = new CommandLineInterface();
        var downloadResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "download-schema",
        ]);

        Assert.Equal(0, downloadResult);

        Assert.True(File.Exists(fileFixture.CombinePath("LibraryCatalog", "Tables", "book.sql")));
        var bookText = await File.ReadAllTextAsync(fileFixture.CombinePath("LibraryCatalog", "Tables", "book.sql"), TestContext.Current.CancellationToken);
        Assert.Contains("CREATE TABLE `book`", bookText);
        Assert.Contains("`book_id` INT NOT NULL AUTO_INCREMENT,", bookText);
        Assert.Contains($"`full_title` VARCHAR(400) GENERATED ALWAYS AS (CONCAT(CONCAT_WS(': ', `title`, `subtitle`), IF({IfConditionBegin}`publication_year` IS NOT NULL{IfConditionEnd}, CONCAT(' (', `publication_year`, ')'), ''))) VIRTUAL,", bookText);
        Assert.Contains("PRIMARY KEY (`book_id`)", bookText);

        Assert.True(File.Exists(fileFixture.CombinePath("LibraryActivity", "Triggers", "tr_active_rental_before_delete.sql")));
        var triggerText = (await File.ReadAllTextAsync(fileFixture.CombinePath("LibraryActivity", "Triggers", "tr_active_rental_before_delete.sql"), TestContext.Current.CancellationToken)).NormalizeWhitespace();
        Assert.Contains("CREATE DEFINER = `admin`@`localhost` TRIGGER `tr_active_rental_before_delete` BEFORE DELETE ON `active_rental` FOR EACH ROW BEGIN", triggerText);

        Assert.True(File.Exists(fileFixture.CombinePath("LibraryActivity", "Functions", "get_late_charge.sql")));
        var functionText = (await File.ReadAllTextAsync(fileFixture.CombinePath("LibraryActivity", "Functions", "get_late_charge.sql"), TestContext.Current.CancellationToken)).NormalizeWhitespace();
        Assert.Contains($"CREATE DEFINER = `admin`@`localhost` FUNCTION `get_late_charge` ({DefaultParameterDirection}`rental_id` INT{DefaultIntWidthString} SIGNED, {DefaultParameterDirection}`lateness_rate` DECIMAL(10,2) SIGNED) RETURNS DECIMAL(10,2) LANGUAGE SQL NOT DETERMINISTIC READS SQL DATA SQL SECURITY DEFINER BEGIN", functionText);
    }

    [Fact]
    public async Task ParseDefinition()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(Path.Combine(CommonHelpers.GetSchemaDirectory("Initial").FullName));

        UpdateConfiguration(fileFixture.RootDirectory.FullName);

        var cli = new CommandLineInterface();
        var parseResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "parse-definition",
            "definition.json"
        ]);

        await TestParseDefinitionCommon(parseResult, fileFixture.RootDirectory.FullName, false, false, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ParseDefinition_ByConfigPath()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(Path.Combine(CommonHelpers.GetSchemaDirectory("Initial").FullName));

        UpdateConfiguration(fileFixture.RootDirectory.FullName);

        var cli = new CommandLineInterface();
        var parseResult = cli.Run([
            "--config",
            fileFixture.CombinePath("database-manager.yaml"),
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "parse-definition",
            "definition.json",
        ]);

        await TestParseDefinitionCommon(parseResult, fileFixture.RootDirectory.FullName, false, false, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ParseDefinition_WithMeta()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(Path.Combine(CommonHelpers.GetSchemaDirectory("Initial").FullName));

        UpdateConfiguration(fileFixture.RootDirectory.FullName);

        var cli = new CommandLineInterface();
        var parseResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "parse-definition",
            "definition.json",
            "--include-meta"
        ]);

        await TestParseDefinitionCommon(parseResult, fileFixture.RootDirectory.FullName, false, true, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ParseDefinition_WithRawText()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(Path.Combine(CommonHelpers.GetSchemaDirectory("Initial").FullName));

        UpdateConfiguration(fileFixture.RootDirectory.FullName);

        var cli = new CommandLineInterface();
        var parseResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "parse-definition",
            "definition.json",
            "--include-raw-text"
        ]);

        await TestParseDefinitionCommon(parseResult, fileFixture.RootDirectory.FullName, true, false, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void ParseDefinition_NoDialect_Fails()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(Path.Combine(CommonHelpers.GetSchemaDirectory("Initial").FullName));

        File.Delete(fileFixture.CombinePath("database-manager.yaml"));

        var cli = new CommandLineInterface();
        var parseResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "parse-definition",
            "definition.json"
        ]);

        Assert.NotEqual(0, parseResult);

        var actual = GetOutputLines(TestOutputHelper);
        var expected = string.Format(CultureInfo.CurrentCulture, MessageTemplates.DialectRequiredTemplate, "parse-definition");
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ParseDefinition_NoConfig_Fails()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(Path.Combine(CommonHelpers.GetSchemaDirectory("Initial").FullName));

        File.Delete(fileFixture.CombinePath("database-manager.yaml"));

        var cli = new CommandLineInterface();
        var parseResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "--dialect",
            Dialect.ToString(),
            "parse-definition",
            "definition.json"
        ]);

        Assert.NotEqual(0, parseResult);

        var actual = GetOutputLines(TestOutputHelper);
        var expected = string.Format(CultureInfo.CurrentCulture, MessageTemplates.ConfigRequiredTemplate, "parse-definition");
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ParseDefinition_NoOutputPath_Fails()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(Path.Combine(CommonHelpers.GetSchemaDirectory("Initial").FullName));

        UpdateConfiguration(fileFixture.RootDirectory.FullName);
        var cli = new CommandLineInterface();
        var parseResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "--dialect",
            Dialect.ToString(),
            "parse-definition",
            ""
        ]);

        Assert.NotEqual(0, parseResult);

        var actual = GetOutputLines(TestOutputHelper);
        var expected = string.Format(CultureInfo.CurrentCulture, ErrorMessages.ErrWithType, ErrorMessages.Err_Cmd, ErrorMessages.Err_Cmd_RequiredArgumentMissing.Replace("{0}", "output_path"));
        Assert.Equal(expected, actual);
    }

    // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
    private async Task TestParseDefinitionCommon(int parseResult, string activeDirectory, bool includeRawText, bool includeMeta, CancellationToken cancellationToken)
    {
        Assert.Equal(0, parseResult);

        var definitionPath = Path.Combine(activeDirectory, "definition.json");
        Assert.True(File.Exists(definitionPath), "Definition file was not created.");

        var definitionText = await File.ReadAllTextAsync(definitionPath, cancellationToken);
        Assert.Contains("\"Tables\"", definitionText);
        Assert.Contains("\"`def`.`library_catalog`.`book`\"", definitionText);

        if (includeRawText)
        {
            Assert.Contains("\"RawBodyText\":", definitionText);
        }
        else
        {
            Assert.DoesNotContain("\"RawBodyText\":", definitionText);
        }
        if (includeMeta)
        {
            Assert.Contains("\"Meta\":", definitionText);
        }
        else
        {
            Assert.DoesNotContain("\"Meta\":", definitionText);
        }

        var definition = Serialization.FromJson<ParsedDefinition<MyDefinition>>(definitionText).Definition;

        Assert.Equal(12, definition.Tables.Count);

        var bookTable = definition.Tables.GetObject("book");
        Assert.Equal(10, bookTable.Columns.Count);

        var bookIdColumn = bookTable.Columns.Single(c => c.Name.Name == "book_id");
        var expectedType = new MyDataType.MyInt() { NumericAttribute = MySqlNumericAttribute.Signed, Width = DefaultIntWidth };
        Assert.Equal(expectedType, bookIdColumn.DataType);
        Assert.True(bookIdColumn.AutoIncrement);

        var fullTitleColumn = bookTable.Columns.Single(c => c.Name.Name == "full_title");
        var expectedStringAttribute = new StringAttribute(DefaultCharset, DefaultCollation);

        Assert.Equal(new MyDataType.MyVarchar(400) { StringAttribute = expectedStringAttribute }, fullTitleColumn.DataType);
        var generated = fullTitleColumn.Generated as ColumnOption.Generated.AsExpression;
        Assert.NotNull(generated);
        Assert.Equal(GenerationMode.Virtual, generated.Mode);

        var activeRentalTable = definition.Tables.GetObject("active_rental", "library_activity");
        Assert.Equal(10, activeRentalTable.Columns.Count);

        Assert.Single(activeRentalTable.UniqueKeys);
        var activeRentalUk = activeRentalTable.UniqueKeys.GetItem("uc_active_rental_book_id");
        Assert.Equal(2, activeRentalUk.Columns.Count);

        Assert.Equal(2, activeRentalTable.ForeignKeys.Count);
        var activeRentalFkBook = activeRentalTable.ForeignKeys.GetItem("fk_active_rental_book");
        Assert.Single(activeRentalFkBook.Columns);

        Assert.Equal(14, definition.Triggers.Count);

        Assert.Single(definition.Procedures);

        Assert.Single(definition.Functions);

        Assert.Single(definition.Views);
    }

    [Fact]
    public void ParseDefinition_FksNoBackingIndex()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(CommonHelpers.GetSchemaDirectory("DefinitionErrorSchemas", "FksNoBackingIndex").FullName);

        UpdateConfiguration(fileFixture.RootDirectory.FullName);

        var cli = new CommandLineInterface();
        var parseResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "--dialect",
            Dialect.ToString(),
            "parse-definition",
            "definition.json"
        ]);

        Assert.NotEqual(0, parseResult);

        var actual = GetOutputLines(TestOutputHelper);
        var expected = string.Format(MessageTemplates.FkBackingIndexTemplate, "`fk_table1`", "`def`.`information_schema`.`table2`");
        Assert.Equal(expected, actual);

        parseResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "--dialect",
            Dialect.ToString(),
            "parse-definition",
            "definition.json",
            "--relaxed"
        ]);

        Assert.Equal(0, parseResult);
    }

    [Fact]
    public void ParseDefinition_ProcNoDefiner()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(CommonHelpers.GetSchemaDirectory("DefinitionErrorSchemas", "ProcNoDefiner").FullName);

        UpdateConfiguration(fileFixture.RootDirectory.FullName);

        var cli = new CommandLineInterface();
        var parseResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "--dialect",
            Dialect.ToString(),
            "parse-definition",
            "definition.json"
        ]);

        Assert.NotEqual(0, parseResult);

        var actual = GetOutputLines(TestOutputHelper);
        var expected = string.Format(MessageTemplates.ProcNoDefinerTemplate, "Procedure", "`def`.`information_schema`.`myproc`");
        Assert.StartsWith(expected, actual);

        parseResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "--dialect",
            Dialect.ToString(),
            "parse-definition",
            "definition.json",
            "--relaxed"
        ]);

        Assert.Equal(0, parseResult);
    }

    [Fact]
    public async Task ParseFiles_NoOptions()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(Path.Combine(CommonHelpers.GetSchemaDirectory("SimpleFilesForParsing").FullName));

        var cli = new CommandLineInterface();
        var parseResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "--dialect",
            Dialect.ToString(),
            "parse-files",
            "select.sql",
            "update.sql",
            "create_function.sql"
        ]);

        await TestParseFilesCommon(parseResult, fileFixture.RootDirectory.FullName, false, false, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ParseFiles_WithMeta()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(Path.Combine(CommonHelpers.GetSchemaDirectory("SimpleFilesForParsing").FullName));

        var cli = new CommandLineInterface();
        var parseResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "parse-files",
            "select.sql",
            "update.sql",
            "create_function.sql",
            "--include-meta"
        ]);

        await TestParseFilesCommon(parseResult, fileFixture.RootDirectory.FullName, true, false, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ParseFiles_DuplicateSkip()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(Path.Combine(CommonHelpers.GetSchemaDirectory("SimpleFilesForParsing").FullName));
        await File.WriteAllTextAsync(Path.Combine(fileFixture.RootDirectory.FullName, "select.parsed.json"), "Existing content", TestContext.Current.CancellationToken);

        var cli = new CommandLineInterface();
        var parseResult1 = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "parse-files",
            "select.sql",
            "update.sql",
            "create_function.sql",
        ]);

        await TestParseFilesCommon(parseResult1, fileFixture.RootDirectory.FullName, false, true, TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(fileFixture.RootDirectory.FullName, "select.parsed.json")));
        var selectText = await File.ReadAllTextAsync(Path.Combine(fileFixture.RootDirectory.FullName, "select.parsed.json"), TestContext.Current.CancellationToken);
        Assert.Equal("Existing content", selectText);
    }

    [Fact]
    public async Task ParseFiles_DuplicateError()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(Path.Combine(CommonHelpers.GetSchemaDirectory("SimpleFilesForParsing").FullName));
        await File.WriteAllTextAsync(Path.Combine(fileFixture.RootDirectory.FullName, "select.parsed.json"), "Existing content", TestContext.Current.CancellationToken);

        var cli = new CommandLineInterface();
        var parseResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
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
        fileFixture.CopyFiles(Path.Combine(CommonHelpers.GetSchemaDirectory("SimpleFilesForParsing").FullName));
        await File.WriteAllTextAsync(Path.Combine(fileFixture.RootDirectory.FullName, "select.parsed.json"), "Existing content", TestContext.Current.CancellationToken);

        var cli = new CommandLineInterface();
        var parseResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "parse-files",
            "select.sql",
            "update.sql",
            "create_function.sql",
            "--file-exists-action",
            "overwrite"
        ]);

        await TestParseFilesCommon(parseResult, fileFixture.RootDirectory.FullName, false, false, TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(fileFixture.RootDirectory.FullName, "select.parsed.json")));
        var selectText = await File.ReadAllTextAsync(Path.Combine(fileFixture.RootDirectory.FullName, "select.parsed.json"), TestContext.Current.CancellationToken);
        Assert.DoesNotContain("Existing content", selectText);
    }

    [Fact]
    public async Task ParseFiles_DuplicateRename()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(Path.Combine(CommonHelpers.GetSchemaDirectory("SimpleFilesForParsing").FullName));
        await File.WriteAllTextAsync(Path.Combine(fileFixture.RootDirectory.FullName, "select.parsed.json"), "Existing content", TestContext.Current.CancellationToken);

        var cli = new CommandLineInterface();
        var parseResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "parse-files",
            "select.sql",
            "update.sql",
            "create_function.sql",
            "--file-exists-action",
            "rename"
        ]);

        await TestParseFilesCommon(parseResult, fileFixture.RootDirectory.FullName, false, false, TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(fileFixture.RootDirectory.FullName, "select.parsed.json.bak.1")));
        var selectText = await File.ReadAllTextAsync(Path.Combine(fileFixture.RootDirectory.FullName, "select.parsed.json.bak.1"), TestContext.Current.CancellationToken);
        Assert.Contains("Existing content", selectText);
    }

    [Fact]
    public void ParseFiles_NonExistentFile()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(Path.Combine(CommonHelpers.GetSchemaDirectory("SimpleFilesForParsing").FullName));

        var cli = new CommandLineInterface();
        var parseResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
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
        fileFixture.CopyFiles(Path.Combine(CommonHelpers.GetSchemaDirectory("SimpleFilesForParsing").FullName));
        // Create two additional SQL files so the glob can expand to 2+ files
        await File.WriteAllTextAsync(fileFixture.CombinePath("extra_a.sql"), "SELECT 1;", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(fileFixture.CombinePath("extra_b.sql"), "SELECT 2;", TestContext.Current.CancellationToken);

        var cli = new CommandLineInterface();
        var parseResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "parse-files",
            "select.sql",       // explicit literal
            "update.sql",       // explicit literal
            "create_function.sql",  // explicit literal
            "extra_*.sql"       // glob: expands to extra_a.sql and extra_b.sql
        ]);

        await TestParseFilesCommon(parseResult, fileFixture.RootDirectory.FullName, false, false, TestContext.Current.CancellationToken);
        Assert.True(File.Exists(fileFixture.CombinePath("extra_a.parsed.json")), "Parsed extra_a file was not created.");
        Assert.True(File.Exists(fileFixture.CombinePath("extra_b.parsed.json")), "Parsed extra_b file was not created.");
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

    [Fact]
    public void ParseErrorFakeJoin()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFile(Path.Combine(CommonHelpers.GetSchemaDirectory("FilesWithErrors").FullName, "parse_error_fake_join.sql"));

        var cli = new CommandLineInterface();
        var parseResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
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
        fileFixture.CopyFile(Path.Combine(CommonHelpers.GetSchemaDirectory("FilesWithErrors").FullName, "parse_error_not_droppable.sql"));

        var cli = new CommandLineInterface();
        var parseResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
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
        fileFixture.CopyFile(Path.Combine(CommonHelpers.GetSchemaDirectory("FilesWithErrors").FullName, "lex_error_unclosed_quote.sql"));

        var cli = new CommandLineInterface();
        var parseResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
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

    [Fact]
    public void DefinitionErrorIndexOnFakeCol()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFile(Path.Combine(CommonHelpers.GetSchemaDirectory("FilesWithErrors").FullName, "def_error_index_on_fake_col.sql"));
        fileFixture.CopyFile(Path.Combine(CommonHelpers.GetSchemaDirectory("FilesWithErrors").FullName, "database-manager.yaml"));

        UpdateConfiguration(fileFixture.RootDirectory.FullName);

        var cli = new CommandLineInterface();
        var parseResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "parse-definition",
            "definition.json",
        ]);

        Assert.NotEqual(0, parseResult);

        var actual = GetOutputLines(TestOutputHelper);
        var expected = string.Format(MessageTemplates.IndexColumnNotFoundTemplate, "`idx_name`", "`def`.`information_schema`.`mytable`", "`fake_column`");
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task FormatSql_InPlace()
    {
        var fileFixture = new FsProjectFixture();
        // Write intentionally unformatted SQL to a file
        const string originalSql = "SELECT col1,col2 FROM table1 WHERE col3 = 'value';";
        await File.WriteAllTextAsync(fileFixture.CombinePath("input.sql"), originalSql, TestContext.Current.CancellationToken);

        var cli = new CommandLineInterface();
        var result = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "format-sql",
            "input.sql"
        ]);

        Assert.Equal(0, result);

        // The original file should have been renamed to a backup
        Assert.True(File.Exists(fileFixture.CombinePath("input.sql.bak.1")), "Backup file was not created.");
        Assert.Equal(originalSql, (await File.ReadAllTextAsync(fileFixture.CombinePath("input.sql.bak.1"), TestContext.Current.CancellationToken)).Trim());

        // The original path should now contain the formatted SQL
        var formattedSql = await File.ReadAllTextAsync(fileFixture.CombinePath("input.sql"), TestContext.Current.CancellationToken);
        Assert.NotEqual(originalSql, formattedSql.Trim());
        Assert.Contains("SELECT", formattedSql);
        Assert.Contains("col1", formattedSql);
        Assert.Contains("col2", formattedSql);
        Assert.Contains("table1", formattedSql);
    }

    [Fact]
    public async Task FormatSql_NoBackup()
    {
        var fileFixture = new FsProjectFixture();
        const string originalSql = "SELECT col1,col2 FROM table1 WHERE col3 = 'value';";
        await File.WriteAllTextAsync(fileFixture.CombinePath("input.sql"), originalSql, TestContext.Current.CancellationToken);

        var cli = new CommandLineInterface();
        var result = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "format-sql",
            "--no-backup",
            "input.sql"
        ]);

        Assert.Equal(0, result);

        // No backup should have been created
        Assert.False(File.Exists(fileFixture.CombinePath("input.sql.bak.1")), "Backup file should not have been created with --no-backup.");

        // The original path should now contain the formatted SQL
        var formattedSql = await File.ReadAllTextAsync(fileFixture.CombinePath("input.sql"), TestContext.Current.CancellationToken);
        Assert.NotEqual(originalSql, formattedSql.Trim());
        Assert.Contains("col1", formattedSql);
    }

    [Fact]
    public async Task FormatSql_WithOutputPattern()
    {
        var fileFixture = new FsProjectFixture();
        const string originalSql = "SELECT col1,col2 FROM table1 WHERE col3 = 'value';";
        await File.WriteAllTextAsync(fileFixture.CombinePath("input.sql"), originalSql, TestContext.Current.CancellationToken);

        var cli = new CommandLineInterface();
        var result = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "format-sql",
            "--output-pattern",
            "{fileName}.formatted.sql",
            "input.sql"
        ]);

        Assert.Equal(0, result);

        // Original file should be unchanged
        Assert.Equal(originalSql, await File.ReadAllTextAsync(fileFixture.CombinePath("input.sql"), TestContext.Current.CancellationToken));

        // A new file with the output pattern name should contain the formatted SQL
        Assert.True(File.Exists(fileFixture.CombinePath("input.formatted.sql")), "Output file was not created.");
        var formattedSql = await File.ReadAllTextAsync(fileFixture.CombinePath("input.formatted.sql"), TestContext.Current.CancellationToken);
        Assert.NotEqual(originalSql, formattedSql.Trim());
        Assert.Contains("col1", formattedSql);
    }

    [Fact]
    public async Task FormatSql_WithDefinition()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(Path.Combine(CommonHelpers.GetSchemaDirectory("Initial").FullName));

        // Write a query targeting a table defined in the Initial schema
        await File.WriteAllTextAsync(fileFixture.CombinePath("query.sql"), "SELECT title FROM book;", TestContext.Current.CancellationToken);

        var cli = new CommandLineInterface();
        var result = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "format-sql",
            "query.sql"
        ]);

        Assert.Equal(0, result);

        var formattedSql = await File.ReadAllTextAsync(fileFixture.CombinePath("query.sql"), TestContext.Current.CancellationToken);
        // The Initial schema uses backtick quoting, so identifiers should be quoted
        Assert.Contains("`book`.`title`", formattedSql);
        Assert.Contains("FROM `book`", formattedSql);
    }

    [Fact]
    public async Task FormatSql_WithAlias_WithDefinition()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(Path.Combine(CommonHelpers.GetSchemaDirectory("Initial").FullName));

        // Write a query targeting a table defined in the Initial schema
        await File.WriteAllTextAsync(fileFixture.CombinePath("query.sql"), "SELECT title FROM book AS b;", TestContext.Current.CancellationToken);

        var cli = new CommandLineInterface();
        var result = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "format-sql",
            "query.sql"
        ]);

        Assert.Equal(0, result);

        var formattedSql = await File.ReadAllTextAsync(fileFixture.CombinePath("query.sql"), TestContext.Current.CancellationToken);
        // The Initial schema uses backtick quoting, so identifiers should be quoted
        Assert.Contains("`b`.`title`", formattedSql);
        Assert.Contains("FROM `book` AS `b`", formattedSql);
    }

    [Fact]
    public async Task FormatSql_WithDefinition_FromDefinitionFile()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(Path.Combine(CommonHelpers.GetSchemaDirectory("Initial").FullName));

        UpdateConfiguration(fileFixture.RootDirectory.FullName);

        // First, parse the definition from the filesystem to produce definition.json
        var cli = new CommandLineInterface();
        var parseResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "parse-definition",
            "definition.json"
        ]);
        Assert.Equal(0, parseResult);
        Assert.True(File.Exists(fileFixture.CombinePath("definition.json")));

        // Write a query targeting a table defined in the Initial schema
        await File.WriteAllTextAsync(fileFixture.CombinePath("query.sql"), "SELECT title FROM book;", TestContext.Current.CancellationToken);

        var formatResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "format-sql",
            "--definition-file",
            "definition.json",
            "query.sql"
        ]);

        Assert.Equal(0, formatResult);

        var formattedSql = await File.ReadAllTextAsync(fileFixture.CombinePath("query.sql"), TestContext.Current.CancellationToken);
        // The Initial schema uses backtick quoting, so identifiers should be quoted
        Assert.Contains("`book`.`title`", formattedSql);
        Assert.Contains("FROM `book`", formattedSql);
    }

    [Fact]
    public async Task FormatSql_WithAlias_WithDefinition_FromDefinitionFile()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(Path.Combine(CommonHelpers.GetSchemaDirectory("Initial").FullName));

        UpdateConfiguration(fileFixture.RootDirectory.FullName);

        // First, parse the definition from the filesystem to produce definition.json
        var cli = new CommandLineInterface();
        var parseResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "parse-definition",
            "definition.json"
        ]);
        Assert.Equal(0, parseResult);
        Assert.True(File.Exists(fileFixture.CombinePath("definition.json")));

        // Write a query targeting a table defined in the Initial schema
        await File.WriteAllTextAsync(fileFixture.CombinePath("query.sql"), "SELECT title FROM book AS b;", TestContext.Current.CancellationToken);

        var formatResult = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "format-sql",
            "--definition-file",
            "definition.json",
            "query.sql"
        ]);

        Assert.Equal(0, formatResult);

        var formattedSql = await File.ReadAllTextAsync(fileFixture.CombinePath("query.sql"), TestContext.Current.CancellationToken);
        // The Initial schema uses backtick quoting, so identifiers should be quoted
        Assert.Contains("`b`.`title`", formattedSql);
        Assert.Contains("FROM `book` AS `b`", formattedSql);
    }

    [Fact]
    public void FormatSql_NonExistentFile()
    {
        var fileFixture = new FsProjectFixture();

        var cli = new CommandLineInterface();
        var result = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "format-sql",
            "nonexistent.sql"
        ]);

        Assert.NotEqual(0, result);

        var actual = GetOutputLines(TestOutputHelper);
        var expectedMessage = $"File not found: {Path.Combine(fileFixture.RootDirectory.FullName, "nonexistent.sql")}";
        Assert.Equal(expectedMessage, actual);
    }

    [Fact]
    public async Task FormatSql_MultipleFiles()
    {
        var fileFixture = new FsProjectFixture();
        const string firstSql = "SELECT col1,col2 FROM table1;";
        const string secondSql = "UPDATE table1 SET col1 = 'val' WHERE col2 = 1;";
        await File.WriteAllTextAsync(fileFixture.CombinePath("first.sql"), firstSql, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(fileFixture.CombinePath("second.sql"), secondSql, TestContext.Current.CancellationToken);

        var cli = new CommandLineInterface();
        var result = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "format-sql",
            "first.sql",
            "second.sql"
        ]);

        Assert.Equal(0, result);

        // Both files should have been formatted (and their backups created)
        Assert.True(File.Exists(fileFixture.CombinePath("first.sql.bak.1")), "Backup for first.sql was not created.");
        Assert.True(File.Exists(fileFixture.CombinePath("second.sql.bak.1")), "Backup for second.sql was not created.");

        var formattedFirst = await File.ReadAllTextAsync(fileFixture.CombinePath("first.sql"), TestContext.Current.CancellationToken);
        var formattedSecond = await File.ReadAllTextAsync(fileFixture.CombinePath("second.sql"), TestContext.Current.CancellationToken);

        Assert.NotEqual(firstSql, formattedFirst.Trim());
        Assert.NotEqual(secondSql, formattedSecond.Trim());
        Assert.Contains("col1", formattedFirst);
        Assert.Contains("col1", formattedSecond);
    }

    [Fact]
    public async Task FormatSql_GlobPattern()
    {
        var fileFixture = new FsProjectFixture();
        const string thirdSql = "SELECT col3 FROM table3;";
        const string querySqlA = "SELECT colA FROM tableA;";
        const string querySqlB = "UPDATE tableB SET colB = 1;";
        await File.WriteAllTextAsync(fileFixture.CombinePath("third.sql"), thirdSql, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(fileFixture.CombinePath("query_a.sql"), querySqlA, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(fileFixture.CombinePath("query_b.sql"), querySqlB, TestContext.Current.CancellationToken);

        var cli = new CommandLineInterface();
        var result = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "format-sql",
            "third.sql",    // explicit literal
            "query_*.sql"   // glob: expands to query_a.sql and query_b.sql
        ]);

        Assert.Equal(0, result);

        Assert.True(File.Exists(fileFixture.CombinePath("third.sql.bak.1")), "Backup for third.sql was not created.");
        Assert.True(File.Exists(fileFixture.CombinePath("query_a.sql.bak.1")), "Backup for query_a.sql was not created.");
        Assert.True(File.Exists(fileFixture.CombinePath("query_b.sql.bak.1")), "Backup for query_b.sql was not created.");
    }

    [Fact]
    public void ValidateConfig_Success()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFile(Path.Combine(CommonHelpers.GetSchemaDirectory("Initial").FullName, "database-manager.yaml"));

        UpdateConfiguration(fileFixture.RootDirectory.FullName);

        var cli = new CommandLineInterface();
        var writer = new StringWriter();
        cli.Out = writer;
        var result = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "validate-config"
        ]);

        Assert.Equal(0, result);

        var actual = writer.ToString().Trim();
        var expected = $"Database connection available: {ConnectionAvailable}";
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ValidateConfig_Success_PrintConfig()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFile(Path.Combine(CommonHelpers.GetSchemaDirectory("Initial").FullName, "database-manager.yaml"));

        UpdateConfiguration(fileFixture.RootDirectory.FullName);

        var cli = new CommandLineInterface();
        var writer = new StringWriter();
        cli.Out = writer;
        var result = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "validate-config",
            "--print-config"
        ]);

        Assert.Equal(0, result);

        var actual = writer.ToString().Trim();
        Assert.Contains($"Dialect: {Dialect}", actual);
        Assert.Contains("FunctionDataRelation: CONTAINS SQL", actual);
        Assert.Contains($"SignedIntWidth: {DefaultIntWidth}", actual);
        Assert.Contains("AllowNamedColumnDefault: false", actual);
        Assert.Contains($"CharacterSet: {DefaultCharset}", actual);
        Assert.Contains("AllowUniqueOnColumn: true", actual);

        Assert.DoesNotContain($"Database connection available: {ConnectionAvailable}", actual);
    }

    [Fact]
    public void ValidateConfig_Success_PrintConfig_WithDeployScript()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFiles(CommonHelpers.GetSchemaDirectory("Test1").FullName);

        UpdateConfiguration(fileFixture.RootDirectory.FullName);

        var cli = new CommandLineInterface();
        var writer = new StringWriter();
        cli.Out = writer;
        var result = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "validate-config",
            "--print-config"
        ]);

        Assert.Equal(0, result);

        var actual = writer.ToString().Trim();
        Assert.Contains(fileFixture.CombinePath("Scripts", "01_preserve_book_info.sql"), actual);
        Assert.Contains(fileFixture.CombinePath("Scripts", "02_preserve_active_rental.sql"), actual);
        Assert.Contains(fileFixture.CombinePath("LibraryActivity", "Tables", "misplaced_script.sql"), actual);
    }

    [Fact]
    public void ValidateConfig_Success_Print_Strict_NoUniqueOnColumn()
    {
        var fileFixture = new FsProjectFixture();
        fileFixture.CopyFile(Path.Combine(CommonHelpers.GetSchemaDirectory("Initial").FullName, "database-manager.yaml"));

        UpdateConfiguration(fileFixture.RootDirectory.FullName);

        var cli = new CommandLineInterface();
        var writer = new StringWriter();
        cli.Out = writer;
        var result = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "validate-config",
            "--print-config",
            "--strict"
        ]);

        Assert.Equal(0, result);

        var actual = writer.ToString().Trim();
        Assert.Contains($"Dialect: {Dialect}", actual);
        Assert.Contains("FunctionDataRelation: CONTAINS SQL", actual);
        Assert.Contains($"SignedIntWidth: {DefaultIntWidth}", actual);
        Assert.Contains("AllowNamedColumnDefault: false", actual);
        Assert.Contains($"CharacterSet: {DefaultCharset}", actual);
        Assert.Contains("AllowUniqueOnColumn: false", actual);

        Assert.DoesNotContain($"Database connection available: {ConnectionAvailable}", actual);
    }


    [Fact]
    public async Task ValidateConfig_Error_InvalidEnum()
    {
        var fileFixture = new FsProjectFixture();
        await File.WriteAllTextAsync(
            fileFixture.CombinePath("database-manager.yaml"),
            string.Format(TestText.BadConfig_Template,
                "NotADialect",
                "false",
                "3",
                ""), TestContext.Current.CancellationToken);

        var cli = new CommandLineInterface();
        var writer = new StringWriter();
        cli.Error = writer;
        var result = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "validate-config",
        ]);

        Assert.NotEqual(0, result);

        var actual = writer.ToString().Trim();
        var expectedMessage = string.Format(ErrorMessages.Err_Conf_RequestedValueNotFound, "NotADialect", "Dialect");
        var expected = string.Format(
            TestText.BadConfig_Error_Template,
            expectedMessage,
            fileFixture.CombinePath("database-manager.yaml"),
            3,
            10,
            "Dialect: NotADialect",
            "         ^"
        );
        Assert.Equal(expected, actual, ignoreWhiteSpaceDifferences: true, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public async Task ValidateConfig_Error_InvalidBoolean()
    {
        var fileFixture = new FsProjectFixture();
        await File.WriteAllTextAsync(
            fileFixture.CombinePath("database-manager.yaml"),
            string.Format(TestText.BadConfig_Template,
                "MySql",
                "NotABoolean",
                "3",
                ""),
            TestContext.Current.CancellationToken);

        var cli = new CommandLineInterface();
        var writer = new StringWriter();
        cli.Error = writer;
        var result = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "validate-config",
        ]);

        Assert.NotEqual(0, result);

        var actual = writer.ToString().Trim();
        var expectedMessage = string.Format(ErrorMessages.Err_Conf_InvalidValue, "NotABoolean", "ObjectNamePrefixWithSchema");
        var expected = string.Format(
            TestText.BadConfig_Error_Template,
            expectedMessage,
            fileFixture.CombinePath("database-manager.yaml"),
            19,
            31,
            "  ObjectNamePrefixWithSchema: NotABoolean",
            "                              ^"
        );
        Assert.Equal(expected, actual, ignoreWhiteSpaceDifferences: true, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public async Task ValidateConfig_Error_InvalidInt()
    {
        var fileFixture = new FsProjectFixture();
        await File.WriteAllTextAsync(
            fileFixture.CombinePath("database-manager.yaml"),
            string.Format(TestText.BadConfig_Template,
                "MySql",
                "false",
                "NotAnInt",
                ""), TestContext.Current.CancellationToken);

        var cli = new CommandLineInterface();
        var writer = new StringWriter();
        cli.Error = writer;
        var result = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "validate-config",
        ]);

        Assert.NotEqual(0, result);

        var actual = writer.ToString().Trim();
        var expectedMessage = string.Format(ErrorMessages.Err_Conf_InvalidValue, "NotAnInt", "SignedIntWidth");
        var expected = string.Format(
            TestText.BadConfig_Error_Template,
            expectedMessage,
            fileFixture.CombinePath("database-manager.yaml"),
            22,
            19,
            "  SignedIntWidth: NotAnInt",
            "                  ^"
        );
        Assert.Equal(expected, actual, ignoreWhiteSpaceDifferences: true, ignoreLineEndingDifferences: true);
    }

    [Fact]
    public async Task ValidateConfig_Error_UnknownField()
    {
        var fileFixture = new FsProjectFixture();
        await File.WriteAllTextAsync(
            fileFixture.CombinePath("database-manager.yaml"),
            string.Format(TestText.BadConfig_Template,
                "MySql",
                "false",
                "4",
                "UnknownField: xyz"), TestContext.Current.CancellationToken);

        var cli = new CommandLineInterface();
        var writer = new StringWriter();
        cli.Error = writer;
        var result = cli.Run([
            "--working-dir",
            fileFixture.RootDirectory.FullName,
            "validate-config",
        ]);

        Assert.NotEqual(0, result);

        var actual = writer.ToString().Trim();
        var expectedMessage = string.Format(ErrorMessages.Err_Conf_UnknownField, "UnknownField");
        var expected = string.Format(
            TestText.BadConfig_Error_Template,
            expectedMessage,
            fileFixture.CombinePath("database-manager.yaml"),
            24,
            1,
            "UnknownField: xyz",
            "^"
        );
        Assert.Equal(expected, actual, ignoreWhiteSpaceDifferences: true, ignoreLineEndingDifferences: true);
    }

    protected void UpdateConfiguration(string dirPath)
    {
        CommonHelpers.UpdateConfiguration(Dialect, dirPath, Container.Hostname, Container.GetMappedPublicPort(Container.GetPort()));
    }

    protected static string GetOutputLines(ITestOutputHelper testOutputHelper)
    {
        var lines = testOutputHelper.Output.Split(Environment.NewLine);
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
