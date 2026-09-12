using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;

namespace TcfOss.DatabaseManager.MySql.IntegrationTests.DownloadSchema;

public abstract class DownloadSchemaTests<TBuilderEntity, TContainerEntity, TFixture> : IClassFixture<TFixture>
    where TFixture : DownloadSchemaFixture<TBuilderEntity, TContainerEntity>
    where TBuilderEntity : IContainerBuilder<TBuilderEntity, TContainerEntity, IContainerConfiguration>, new()
    where TContainerEntity : IContainer, IDatabaseContainer
{
    protected DownloadSchemaFixture<TBuilderEntity, TContainerEntity> Fixture { get; init; } = null!;
    protected virtual string DefaultCharacterSetAndCollation => "CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci";
    protected virtual string DefaultFunctionParameterDirection => "";
    protected virtual string DefaultIntegerWidthString => "";
    private static string DefaultNumericAttributeString => " SIGNED";
    protected virtual string IfClauseOpen => "";
    protected virtual string IfClauseClose => "";

    [Fact]
    public void Test_Book_Table()
    {
        var bookPath = Path.Combine(Fixture.RootDirectory.FullName, "LibraryCatalog", "Tables", "book.sql");
        Assert.True(File.Exists(bookPath), "Book table SQL file does not exist.");

        var bookContent = File.ReadAllText(bookPath);
        Assert.Contains("CREATE TABLE `book`", bookContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("`book_id` INT NOT NULL AUTO_INCREMENT,", bookContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PRIMARY KEY (`book_id`)", bookContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("`title` VARCHAR(255) NOT NULL,", bookContent, StringComparison.OrdinalIgnoreCase);

        string expectedGenerationExpression = $"CONCAT(CONCAT_WS(': ', `title`, `subtitle`), IF({IfClauseOpen}`publication_year` IS NOT NULL{IfClauseClose}, CONCAT(' (', `publication_year`, ')'), ''))";

        Assert.Contains($"`full_title` VARCHAR(400) GENERATED ALWAYS AS ({expectedGenerationExpression}) VIRTUAL,", bookContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Test_Book_Author_Table()
    {
        var bookAuthorPath = Path.Combine(Fixture.RootDirectory.FullName, "LibraryCatalog", "Tables", "book_author.sql");
        Assert.True(File.Exists(bookAuthorPath), "Book Author table SQL file does not exist.");

        var bookAuthorContent = File.ReadAllText(bookAuthorPath);
        Assert.Contains("CREATE TABLE `book_author`", bookAuthorContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("`book_id` INT NOT NULL,", bookAuthorContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("`contributor_id` INT NOT NULL,", bookAuthorContent, StringComparison.OrdinalIgnoreCase);

        string? onDelete = null;
        if (!Fixture.MyConfig.Formatting.OmitModifiersIfDefault || Fixture.MyConfig.AttributeDefaults.ForeignKeyOnDelete != ReferentialAction.Restrict)
        {
            onDelete = " ON DELETE RESTRICT";
        }

        Assert.Contains("PRIMARY KEY (`book_id`, `contributor_id`),", bookAuthorContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"CONSTRAINT `fk_book_author_book` FOREIGN KEY (`book_id`) REFERENCES `book` (`book_id`){onDelete} ON UPDATE CASCADE,", bookAuthorContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"CONSTRAINT `fk_book_author_contributor` FOREIGN KEY (`contributor_id`) REFERENCES `contributor` (`contributor_id`){onDelete} ON UPDATE CASCADE", bookAuthorContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Test_Stale_Users_Event()
    {
        var eventPath = Path.Combine(Fixture.RootDirectory.FullName, "LibraryIdentity", "Events", "deactivate_stale_users.sql");
        Assert.True(File.Exists(eventPath), "Deactivate Stale Users event SQL file does not exist.");

        var eventLines = File.ReadAllLines(eventPath);

        Assert.Equal("CREATE", eventLines[2].Trim());
        Assert.Equal("DEFINER = `admin`@`localhost`", eventLines[3].Trim());
        Assert.Equal("EVENT `deactivate_stale_users`", eventLines[4].Trim());
        Assert.Equal("ON SCHEDULE EVERY 1 DAY STARTS '2024-01-01 00:00:00'", eventLines[5].Trim());
        Assert.Equal("ON COMPLETION NOT PRESERVE", eventLines[6].Trim());
        Assert.Equal("ENABLE", eventLines[7].Trim());
        Assert.Equal("DO", eventLines[8].Trim());
        Assert.Equal("BEGIN", eventLines[9].Trim());

        Assert.StartsWith("END", eventLines[^3].Trim());
        Assert.Equal("DELIMITER ;", eventLines[^1].Trim());
    }

    [Fact]
    public void Test_Search_Available_Books_Procedure()
    {
        var procPath = Path.Combine(Fixture.RootDirectory.FullName, "LibraryActivity", "Procedures", "search_available_books.sql");
        Assert.True(File.Exists(procPath), "Search Available Books procedure SQL file does not exist.");

        var procContent = File.ReadAllLines(procPath);

        var skipLines = 2;
        string[] expectedLines = [
            "CREATE",
            "DEFINER = `admin`@`localhost`",
            $"PROCEDURE `search_available_books` (IN `search_term` VARCHAR(255) {DefaultCharacterSetAndCollation})",
            "LANGUAGE SQL NOT DETERMINISTIC CONTAINS SQL SQL SECURITY DEFINER",
            "BEGIN",
            "",
            "SELECT",
            "ab.book_id,"
        ];

        for (var i = 0; i < expectedLines.Length; i++)
        {
            Assert.Equal(expectedLines[i], procContent[i + skipLines].Trim());
        }

        Assert.StartsWith("END", procContent[^3].Trim());
        Assert.Equal("DELIMITER ;", procContent[^1].Trim());
    }

    [Fact]
    public void Test_Get_Late_Charge_Function()
    {
        var funcPath = Path.Combine(Fixture.RootDirectory.FullName, "LibraryActivity", "Functions", "get_late_charge.sql");
        Assert.True(File.Exists(funcPath), "Get Late Charge function SQL file does not exist.");

        var funcContent = File.ReadAllLines(funcPath);

        var skipLines = 2;
        string[] expectedLines = [
            "CREATE",
            "DEFINER = `admin`@`localhost`",
            $"FUNCTION `get_late_charge` ({DefaultFunctionParameterDirection}`rental_id` INT{DefaultIntegerWidthString}{DefaultNumericAttributeString}, {DefaultFunctionParameterDirection}`lateness_rate` DECIMAL(10,2){DefaultNumericAttributeString})",
            "RETURNS DECIMAL(10,2)",
            "LANGUAGE SQL NOT DETERMINISTIC READS SQL DATA SQL SECURITY DEFINER",
            "BEGIN",
            "",
            "DECLARE late_charge DECIMAL(10,2) DEFAULT 0.00;",
        ];

        for (var i = 0; i < expectedLines.Length; i++)
        {
            Assert.Equal(expectedLines[i], funcContent[i + skipLines].Trim());
        }

        Assert.StartsWith("END", funcContent[^3].Trim());
        Assert.Equal("DELIMITER ;", funcContent[^1].Trim());
    }

    [Fact]
    public void Test_Genre_Before_Update_Trigger()
    {
        var triggerPath = Path.Combine(Fixture.RootDirectory.FullName, "LibraryCatalog", "Triggers", "tr_genre_before_update.sql");
        Assert.True(File.Exists(triggerPath), "Genre Before Update trigger SQL file does not exist.");

        var triggerContent = File.ReadAllLines(triggerPath);

        var skipLines = 2;
        string[] expectedLines = [
            "CREATE",
            "DEFINER = `admin`@`localhost`",
            "TRIGGER `tr_genre_before_update`",
            "BEFORE UPDATE",
            "ON `genre`",
            "FOR EACH ROW",
            "BEGIN",
            "",
            "DECLARE parent_name VARCHAR(25);",
        ];

        for (var i = 0; i < expectedLines.Length; i++)
        {
            Assert.Equal(expectedLines[i], triggerContent[i + skipLines].Trim());
        }

        Assert.StartsWith("END", triggerContent[^3].Trim());
        Assert.Equal("DELIMITER ;", triggerContent[^1].Trim());
    }

    [Fact]
    public void Test_Available_Books_View()
    {
        var viewPath = Path.Combine(Fixture.RootDirectory.FullName, "LibraryActivity", "Views", "available_books.sql");
        Assert.True(File.Exists(viewPath), "Available Books view SQL file does not exist.");

        var viewContent = File.ReadAllLines(viewPath);

        var skipLines = 0;
        string[] expectedLines = [
            "CREATE",
            "ALGORITHM = UNDEFINED",
            "DEFINER = `admin`@`localhost`",
            "SQL SECURITY INVOKER",
            "VIEW `available_books`",
            "AS",
        ];

        for (var i = 0; i < expectedLines.Length; i++)
        {
            Assert.Equal(expectedLines[i], viewContent[i + skipLines].Trim());
        }

        Assert.StartsWith("SELECT", viewContent[^1]);
        Assert.EndsWith(";", viewContent[^1]);
    }
}
