using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Tests.DefinitionBuilding;
using TcfOss.DatabaseManager.MySql.BuiltIn;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DefinitionBuilding;
using TcfOss.DatabaseManager.MySql.Lexing;
using TcfOss.DatabaseManager.MySql.Parsing;

namespace TcfOss.DatabaseManager.MySql.Tests.DefinitionBuilding;


public class MyFsDefinitionLoaderTests
{
    [Fact]
    public void ValidSchemaWorks()
    {
        var definitionLoader = GetLoader();

        var definition = definitionLoader.LoadDefinition(relaxed: false);

        Assert.NotNull(definition);

        Assert.Equal(5, definition.Tables.Count);

        var booksTable = definition.Tables.Values.Single(t => t.Name.Name == "books");
        Assert.Equal(3, booksTable.Columns.Count);
        Assert.NotNull(booksTable.PrimaryKey);

        var bookAuthorsTable = definition.Tables.Values.Single(t => t.Name.Name == "book_authors");
        Assert.Equal(2, bookAuthorsTable.Columns.Count);
        Assert.NotNull(bookAuthorsTable.PrimaryKey);
        Assert.Equal(2, bookAuthorsTable.ForeignKeys.Count);
    }

    [Fact]
    public void LexErrorThrows()
    {
        var invalidBookText = """
        CREATE TABLE books (
            book_id INT NOT NULL AUTO_INCREMENT,
            title VARCHAR(255) NOT NULL DEFAULT ',
            published_date DATE,
            PRIMARY KEY (book_id)
        );
        """;

        var definitionLoader = GetLoader(bookOverride: invalidBookText);

        var exception = Assert.Throws<LexException.WithText>(() => definitionLoader.LoadDefinition(relaxed: false));
        var expectedMessage = """
        Lexing Error: Unterminated string literal. (In file 'books.sql' at line 3, column 41)
            title VARCHAR(255) NOT NULL DEFAULT ',
                                                ^
        """;
        Assert.Equal(expectedMessage, exception.Message, ignoreLineEndingDifferences: true, ignoreWhiteSpaceDifferences: true);
    }

    [Fact]
    public void ParseErrorThrows()
    {
        var invalidBookText = """
        CREATE TABLE books (
            book_id INT NOT NULL AUTO_INCREMENT,
            title VARCHAR(255) NOT NULL,
            published_date DATE
            PRIMARY KEY (book_id)
        );
        """;
        var definitionLoader = GetLoader(bookOverride: invalidBookText);

        var exception = Assert.Throws<ParseException.WithText>(() => definitionLoader.LoadDefinition(relaxed: false));
        var expectedMessage = """
        Parse Error: Expected one of { ',' | ')' }. Found '('. (In file 'books.sql' at line 5, column 17)
            PRIMARY KEY (book_id)
                        ^
        """;
        Assert.Equal(expectedMessage, exception.Message, ignoreLineEndingDifferences: true);
    }

    private static MyFsDefinitionLoader GetLoader(
        string? bookOverride = null,
        string? contributorOverride = null,
        string? bookAuthorsOverride = null,
        string? usersOverride = null,
        string? rentalsOverride = null)
    {
        var loggerFactory = new LoggerFactory();
        var fileLoaderLogger = loggerFactory.CreateLogger<DefinitionFileLoader<MySchemaMapping>>();
        var config = TestConfig.GetMyTestConfig();
        var fileLoader = new FakeFileLoader<MySchemaMapping>(config, fileLoaderLogger, s_files, (x) => GetText(x, bookOverride, contributorOverride, bookAuthorsOverride, usersOverride, rentalsOverride));
        var textParser = new TextParser(new MyLexer(), new MyParser());

        var definitionLoaderLogger = loggerFactory.CreateLogger<MyFsDefinitionLoader>();
        return new MyFsDefinitionLoader(config, fileLoader, textParser, new SourceManager(), new MyFunctionNameProvider(), definitionLoaderLogger, loggerFactory);
    }

    private static readonly List<string> s_files =
    [
        "/fake/path/to/schema1/books.sql",
        "/fake/path/to/schema1/contributors.sql",
        "/fake/path/to/schema1/book_authors.sql",
        "/fake/path/to/schema2/subdir/users.sql",
        "/fake/path/to/schema2/rentals.sql",
    ];

    private const string BookText = @"
        CREATE TABLE books (
            book_id INT NOT NULL AUTO_INCREMENT,
            title VARCHAR(255) NOT NULL,
            published_date DATE,
            PRIMARY KEY (book_id)
        );
    ";

    private const string ContributorText = @"
        CREATE TABLE contributors (
            contributor_id INT NOT NULL AUTO_INCREMENT,
            name VARCHAR(255) NOT NULL,
            PRIMARY KEY (contributor_id)
        );
    ";

    private const string BookAuthorsText = @"
        CREATE TABLE book_authors (
            book_id INT NOT NULL,
            contributor_id INT NOT NULL,
            PRIMARY KEY (book_id, contributor_id),
            INDEX idx_book_authors_contributor_id (contributor_id),
            CONSTRAINT fk_book_authors_book_id FOREIGN KEY (book_id) REFERENCES books(book_id),
            CONSTRAINT fk_book_authors_contributor_id FOREIGN KEY (contributor_id) REFERENCES contributors(contributor_id)
        );
    ";

    private const string UsersText = @"
        CREATE TABLE users (
            user_id INT NOT NULL AUTO_INCREMENT,
            username VARCHAR(100) NOT NULL,
            email VARCHAR(255) NOT NULL,
            PRIMARY KEY (user_id),
            CONSTRAINT uc_users_email UNIQUE (email),
            CONSTRAINT uc_users_username UNIQUE (username)
        );
    ";

    private const string RentalsText = @"
        CREATE TABLE rentals (
            rental_id INT NOT NULL AUTO_INCREMENT,
            user_id INT NOT NULL,
            book_id INT NOT NULL,
            rental_date DATE NOT NULL,
            return_date DATE,
            PRIMARY KEY (rental_id),
            INDEX idx_rentals_user_id (user_id),
            INDEX idx_rentals_book_id (book_id),
            CONSTRAINT fk_rentals_user_id FOREIGN KEY (user_id) REFERENCES users(user_id),
            CONSTRAINT fk_rentals_book_id FOREIGN KEY (book_id) REFERENCES schema1.books(book_id)
        );
    ";

    private static string GetText(
        string filePath,
        string? bookOverride = null,
        string? contributorOverride = null,
        string? bookAuthorsOverride = null,
        string? usersOverride = null,
        string? rentalsOverride = null
    ) => filePath switch
    {
        "/fake/path/to/schema1/books.sql" => bookOverride ?? BookText,
        "/fake/path/to/schema1/contributors.sql" => contributorOverride ?? ContributorText,
        "/fake/path/to/schema1/book_authors.sql" => bookAuthorsOverride ?? BookAuthorsText,
        "/fake/path/to/schema2/subdir/users.sql" => usersOverride ?? UsersText,
        "/fake/path/to/schema2/rentals.sql" => rentalsOverride ?? RentalsText,
        _ => throw new ArgumentException($"Unexpected file path: {filePath}"),
    };
}
