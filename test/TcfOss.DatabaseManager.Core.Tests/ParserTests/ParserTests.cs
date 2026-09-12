using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Tests.ParserTests;

// ReSharper disable once ClassNeverInstantiated.Global
public class ParserTests : ParserTestsBase<GenericLexer, Parser>
{
    [Theory]
    [InlineData("SELECT * FROM mytable")]
    /* Select - Joins */
    [InlineData("SELECT a.*, b.* FROM table1 AS a INNER JOIN table2 AS b ON a.id = b.id WHERE a.name >= 'Jones' OR b.category IN (1, 2, 5) AND b.name <= 'Smith'", Label = "SELECT complex joins and filters")]
    [InlineData("SELECT * FROM table1")]
    [InlineData("SELECT a + b, a - b, a * b, a / b FROM table1 AS t1 INNER JOIN table2 AS t2 ON t1.a = t2.id LEFT OUTER JOIN table3 AS t3 ON t2.b = t3.id")]
    [InlineData("SELECT a + b, a - b, a * b, a / b FROM table1 AS t1 INNER JOIN table2 AS t2 ON t1.a = t2.id RIGHT OUTER JOIN table3 AS t3 ON t2.b = t3.id")]
    [InlineData("SELECT a + b, a - b, a * b, a / b FROM table1 AS t1 INNER JOIN table2 AS t2 ON t1.a = t2.id FULL OUTER JOIN table3 AS t3 ON t2.b = t3.id")]
    [InlineData("SELECT a.*, b.* FROM table1 AS a INNER JOIN table2 AS b ON a.id = b.id WHERE (a.name >= 'Jones' OR b.category IN (1, 2, 5)) AND b.name <= 'Smith'")]
    [InlineData("SELECT a.*, b.* FROM table1 AS a CROSS JOIN table2 AS b")]
    [InlineData("SELECT a.*, b.* FROM table1 AS a CROSS APPLY table2 AS b")]
    [InlineData("SELECT a.*, b.* FROM table1 AS a OUTER APPLY table2 AS b")]
    [InlineData("SELECT a.*, b.* FROM table1 AS a CROSS APPLY (SELECT col1, col2 FROM table_2) AS b")]
    [InlineData("SELECT a.*, b.* FROM table1 AS a NATURAL INNER JOIN table2 AS b")]
    [InlineData("SELECT a.*, b.* FROM table1 AS a INNER JOIN table2 AS b USING (id, name)")]
    /* Select - Into */
    [InlineData("SELECT a INTO myvar FROM old_table")]
    [InlineData("SELECT a, b INTO var1, var2 FROM old_table")]
    [InlineData("SELECT a, b INTO new_table FROM old_table")]
    [InlineData("SELECT a, b INTO new_table FROM old_table WHERE a > 10")]
    [InlineData("SELECT a, b INTO new_table FROM old_table WHERE a > 10 ORDER BY b DESC")]
    [InlineData("SELECT a, b INTO new_schema.new_table FROM old_table")]
    [InlineData("SELECT a, b INTO new_schema.new_table FROM old_table WHERE a > 10")]
    [InlineData("SELECT a, b INTO new_schema.new_table FROM old_table WHERE a > 10 ORDER BY b DESC")]
    [InlineData("SELECT a, b INTO OUTFILE 'output.txt' FROM old_table")]
    [InlineData("SELECT a FROM old_table INTO myvar")]
    [InlineData("SELECT a, b FROM old_table INTO var1, var2")]
    [InlineData("SELECT a, b FROM old_table INTO new_table")]
    [InlineData("SELECT a, b FROM old_table INTO new_schema.new_table")]
    [InlineData("SELECT a, b FROM old_table INTO OUTFILE 'output.txt'")]
    [InlineData("SELECT DISTINCT a, b FROM mytable")]
    [InlineData("SELECT t.name, COUNT(*) FROM table1 AS t GROUP BY t.name HAVING COUNT(*) > 5")]
    /* Select - Sets */
    [InlineData("SELECT a, b FROM table1 UNION SELECT c, d FROM table2")]
    [InlineData("SELECT a, b FROM table1 INTERSECT DISTINCT SELECT c, d FROM table2")]
    /* Select - CTE */
    [InlineData("WITH book_authors AS (SELECT person_id, name FROM authors) SELECT * FROM book_authors")]
    [InlineData("WITH RECURSIVE expanded_genres AS (SELECT * FROM genres AS g WHERE g.parent_id IS NULL UNION ALL SELECT * FROM genres AS gsub INNER JOIN expanded_genres AS eg ON gsub.parent_id = eg.id) SELECT * FROM expanded_genres", Label = "WITH RECURSIVE expanded_genres")]
    /* INSERT */
    [InlineData("INSERT INTO mytable (col1, col2, col3) VALUES ('v1', 'v2', 'v3'), ('v4', 'v5', 'v6'), ('v7', 'v8', 'v9')")]
    /* INSERT - CTE */
    [InlineData("WITH mycte AS (SELECT * FROM mytable) INSERT INTO mytable SELECT t.a, t.b, t.c FROM mycte AS t")]
    /* UPDATE - CTE */
    [InlineData("WITH mycte AS (SELECT * FROM mytable) UPDATE othertable AS ot SET ot.val = mycte.val WHERE mycte.id = ot.id")]
    /* DELETE - CTE */
    [InlineData("WITH mycte AS (SELECT * FROM mytable) DELETE FROM othertable AS ot WHERE mycte.id = ot.id")]
    /* IF block */
    /* DECLARE */
    [InlineData("DECLARE mycon CONDITION FOR SQLSTATE 'ABCDE'")]
    [InlineData("DECLARE mycon CONDITION FOR 1234")]
    [InlineData("DECLARE CONTINUE HANDLER FOR 1234 SELECT 1")]
    [InlineData("DECLARE CONTINUE HANDLER FOR SQLSTATE '42S02', SQLWARNING SELECT 1")]
    [InlineData("DECLARE EXIT HANDLER FOR SQLEXCEPTION, NOT FOUND, 1234 BEGIN SELECT 1; END")]
    [InlineData("DECLARE UNDO HANDLER FOR mycon BEGIN SELECT 1; SELECT 2; END")]
    [InlineData("DECLARE mycur CURSOR FOR SELECT col1, col2, col3 FROM mytable")]
    /* SET VARIABLE */
    [InlineData("SET myvar = 3")]
    [InlineData("SET myvar = a + b")]
    [InlineData("SET qual.myvar = 'string'")]
    [InlineData("SET myvar = (SELECT col1 FROM mytable WHERE col2 > 4 LIMIT 1)")]
    [InlineData("SET myvar1 = 3, myvar2 = 'string', myvar3 = a + b, myvar4 = (SELECT col1 FROM mytable WHERE col2 > 4 LIMIT 1)")]
    /* USE */
    [InlineData("USE mydatabase")]
    [InlineData("USE DATABASE mydatabase")]
    [InlineData("USE SCHEMA myschema")]
    [InlineData("USE CATALOG mycatalog")]
    [InlineData("USE mycatalog.myschema")]
    public static void Statement_TextMatch(string sql)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.Parse, sql);

        Assert.Equal(expected, actual);
    }

    private static Select GetStatementWithNonSqlTestStatement()
    {
        //       = "0    5   10    15   20   25   30   35    40   45   50   55    60   65";
        //       = "|    |    |     |    |    |    |    |     |    |    |    |     |    |";
        var text = "/* get data */\nSELECT a, b FROM mytable;\n --done getting data\n";

        var tokens = GetTokens(text);
        var actualStatements = new Parser().Parse(tokens);

        Assert.Equal(2, actualStatements.Count);
        var select = Assert.IsType<Select>(actualStatements[0]);
        Assert.IsType<InertOnly>(actualStatements[1]);

        return select;
    }

    [Fact]
    public static void Select_With_Non_Sql_Statement()
    {
        var actualSelect = GetStatementWithNonSqlTestStatement();

        var expectedSelect = new Select(
            new SelectBody.SimpleSelectQuery(
                new SimpleSelect(
                [
                    new SimpleSelectItem.UnnamedExpression(new SingleIdentifier(new Identifier("a"))),
                    new SimpleSelectItem.UnnamedExpression(new SingleIdentifier(new Identifier("b")))
                ])
                {
                    From = [new TableWithJoins(new TableFactor.Table(new ObjectName([new Identifier("mytable")])))]
                }
            )
        );

        Assert.Equal(expectedSelect, actualSelect);
    }

    [Fact]
    public static void Select_With_Non_Sql_Pre_Sql()
    {
        var actualSelect = GetStatementWithNonSqlTestStatement();

        var actualPreSql = actualSelect.Meta.PreNonSql;
        var expectedPreSql = new List<NonSql>()
        {
            new NonSql.BlockComment(" get data "),
            new NonSql.Newlines()
        };

        Assert.Equal(expectedPreSql, actualPreSql);
    }

    [Fact]
    public static void Select_With_Non_Sql_Post_Sql()
    {
        var actualSelect = GetStatementWithNonSqlTestStatement();

        // Trailing non-SQL after the semicolon now lives in an InertOnly statement's
        // PreNonSql rather than in the preceding statement's PostNonSql.
        Assert.Null(actualSelect.Meta.PostNonSql);
    }

    [Fact]
    public static void InertOnly_Has_Trailing_NonSql()
    {
        var text = "/* get data */\nSELECT a, b FROM mytable;\n --done getting data\n";
        var tokens = GetTokens(text);
        var actualStatements = new Parser().Parse(tokens);

        var inertOnly = (InertOnly)actualStatements[1];

        var expectedPreSql = new List<NonSql>()
        {
            new NonSql.Newlines(),
            new NonSql.Spaces(1),
            new NonSql.LineComment("done getting data\n")
        };

        Assert.Equal(expectedPreSql, inertOnly.Meta.PreNonSql);
    }

    [Fact]
    public static void Create_Unknown_Throws()
    {
        var lexer = new GenericLexer();
        var parser = new Parser();
        var tokens = lexer.Tokenize("CREATE UNKNOWN OBJECT");
        var exception = Assert.Throws<ParseException.ExpectedOneOfButFound>(() => parser.Parse([.. tokens]));
        var expectedMessage = "Expected one of { TABLE | TRIGGER | VIEW | FUNCTION | PROCEDURE | EVENT | INDEX }. Found UNKNOWN.";

        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public static void NonSqlContainer_Whitespace()
    {
        var lexer = new GenericLexer();
        var tokens = lexer.Tokenize("\n \t\r    \r\n\t  \n");

        var first = tokens[0] as Lexing.Tokens.NonSqlContainer;
        Assert.NotNull(first);

        var asStatement = first.SubTokens.ToStatementNonSql();

        var expected = new List<NonSql>()
        {
            new NonSql.Newlines(),
            new NonSql.Spaces(1),
            new NonSql.Tabs(),
            new NonSql.Newlines(),
            new NonSql.Spaces(4),
            new NonSql.Newlines(),
            new NonSql.Tabs(),
            new NonSql.Spaces(2),
            new NonSql.Newlines()
        };

        Assert.Equal(expected, asStatement);

        Assert.Equal("\n \t\n    \n\t  \n", asStatement.ToSqlDelimited(""));
    }

    [Fact]
    public static void NonSqlContainer_Comments()
    {
        var lexer = new GenericLexer();
        var tokens = lexer.Tokenize("/* block comment */ -- line comment\n\t\t/* another block */");

        var first = tokens[0] as Lexing.Tokens.NonSqlContainer;
        Assert.NotNull(first);

        var asStatement = first.SubTokens.ToStatementNonSql();

        var expected = new List<NonSql>()
        {
            new NonSql.BlockComment(" block comment "),
            new NonSql.Spaces(1),
            new NonSql.LineComment(" line comment\n"),
            new NonSql.Tabs(2),
            new NonSql.BlockComment(" another block ")
        };

        Assert.Equal(expected, asStatement);

        Assert.Equal("/* block comment */ -- line comment\n\t\t/* another block */", asStatement.ToSqlDelimited(""));
    }
}
