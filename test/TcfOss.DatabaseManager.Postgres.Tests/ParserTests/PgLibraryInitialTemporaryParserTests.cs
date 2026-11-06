using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.Postgres.Lexing;
using TcfOss.DatabaseManager.Postgres.Parsing;

namespace TcfOss.DatabaseManager.Postgres.Tests.ParserTests;

public class PgLibraryInitialTemporaryParserTests : ParserTestsBase<PgLexer, PgParser>
{
    [Theory]
    [MemberData(nameof(SqlFiles))]
    public void InitialFixtureFiles_Parse(string relativePath)
    {
        string sql = File.ReadAllText(Path.Combine(GetFixtureDirectory(), relativePath));

        Assert.NotEmpty(ParseStatements(sql));
    }

    [Theory]
    [InlineData("LibraryActivity/Tables/active_rental.sql", "library_activity.active_rental.really_active",
        "If null, the book is available to loan but cannot be deleted until fees are paid.")]
    [InlineData("LibraryActivity/Tables/rental_log.sql", "library_activity.rental_log.rental_id",
        "Fake foreign key to allow active_rental to be deleted")]
    public void InitialFixtureComments_PreserveTargetsAndText(string relativePath, string expectedTarget, string expectedText)
    {
        string sql = File.ReadAllText(Path.Combine(GetFixtureDirectory(), relativePath));

        CommentOn comment = Assert.Single(ParseStatements(sql).OfType<CommentOn>());

        var target = Assert.IsType<PgStatementCommentTarget.Column>(comment.Target);
        Assert.Equal(expectedTarget, target.Name.ToSql());
        Assert.Equal(expectedText, Assert.IsType<Value.SingleQuotedString>(comment.Comment).Value);
    }

    public static TheoryData<string> SqlFiles()
    {
        string directory = GetFixtureDirectory();
        var files = new TheoryData<string>();
        foreach (string file in Directory.EnumerateFiles(directory, "*.sql", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            files.Add(Path.GetRelativePath(directory, file));
        }
        Assert.NotEmpty(files);
        return files;
    }

    private static string GetFixtureDirectory()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "DatabaseManager.slnx")))
        {
            directory = directory.Parent;
        }
        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "test", "Resources", "TestSchemas", "Postgres", "Library", "Initial");
    }
}
