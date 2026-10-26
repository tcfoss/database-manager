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
    [InlineData("SELECT a, b FROM (SELECT a, b FROM mytable)")]
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
    [InlineData("SELECT a, b FROM table1 UNION ALL SELECT c, d FROM table2")]
    [InlineData("SELECT a, b FROM table1 INTERSECT DISTINCT SELECT c, d FROM table2")]
    [InlineData("SELECT a, b FROM table1 INTERSECT ALL SELECT c, d FROM table2")]
    [InlineData("SELECT a, b FROM table1 EXCEPT SELECT c, d FROM table2")]
    [InlineData("SELECT a, b FROM table1 EXCEPT ALL SELECT c, d FROM table2")]
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

    [Theory]
    [InlineData("SELECT a UNION SELECT b LIMIT 1 INTO result")]
    [InlineData("(SELECT a) LIMIT 1 INTO result")]
    [InlineData("(SELECT a) UNION SELECT b LIMIT 1 INTO result")]
    [InlineData("SELECT a UNION SELECT b UNION SELECT c LIMIT 1 INTO result")]
    [InlineData("SELECT a INTO result FROM source")]
    [InlineData("SELECT a, b INTO result FROM source")]
    [InlineData("SELECT a FROM source INTO result")]
    [InlineData("SELECT a, b FROM source INTO result")]
    public void SelectInto_SingleName_DefaultsToTableTarget(string sql)
    {
        var select = Assert.IsType<Select>(ParseStatement(sql));
        SelectInto? target = select.SelectInto ?? (select.Body as SelectBody.SimpleSelectQuery)?.Query.SelectInto;

        var into = Assert.IsType<SelectInto.SelectIntoTable>(target);
        Assert.Equal(new ObjectName([new Identifier("result")]), into.Name);
    }

    [Fact]
    public void Cte_OptionalFrom_IsPreserved()
    {
        var select = Assert.IsType<Select>(ParseStatement("WITH recent AS (SELECT book_id FROM book) FROM previous SELECT book_id FROM recent"));

        CommonTableExpressionBody body = Assert.Single(select.CommonTableExpression!.Tables);
        Assert.Equal("previous", body.From!.Name);
    }

    [Fact]
    public void MultiplyNestedJoin_IsParsed()
    {
        var select = Assert.IsType<Select>(ParseStatement("SELECT a.id FROM ((table1 AS a INNER JOIN table2 AS b ON a.id = b.id))"));

        var body = Assert.IsType<SelectBody.SimpleSelectQuery>(select.Body);
        TableWithJoins from = Assert.Single(body.Query.From!);
        var nested = Assert.IsType<TableFactor.NestedJoin>(from.Relation);
        Assert.Null(nested.TableWithJoins.Joins);
        var inner = Assert.IsType<TableFactor.NestedJoin>(nested.TableWithJoins.Relation);
        Assert.Single(inner.TableWithJoins.Joins!);
    }

    [Theory]
    [InlineData("SELECT * FROM table1 CROSS invalid_join table2")]
    [InlineData("SELECT * FROM table1 OUTER JOIN table2")]
    [InlineData("SELECT * FROM table1 NATURAL OUTER JOIN table2")]
    [InlineData("SELECT * FROM table1 NATURAL invalid_join table2")]
    public void InvalidJoinType_ThrowsParseException(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatement(sql));
    }

    [Fact]
    public void DerivedTable_WithCte_PreservesCteAndAlias()
    {
        var select = Assert.IsType<Select>(ParseStatement("SELECT derived.id FROM (WITH recent AS (SELECT id FROM source) SELECT id FROM recent) AS derived"));

        var body = Assert.IsType<SelectBody.SimpleSelectQuery>(select.Body);
        var derived = Assert.IsType<TableFactor.Derived>(Assert.Single(body.Query.From!).Relation);
        Assert.Equal("derived", derived.Alias!.Name);
        CommonTableExpressionBody cte = Assert.Single(derived.SubQuery.CommonTableExpression!.Tables);
        Assert.Equal("recent", cte.Name.Name);
    }

    [Fact]
    public void ParenthesizedTableWithoutJoin_Throws()
    {
        var exception = Assert.Throws<ParseException.ExpectedButFound>(() => ParseStatement("SELECT id FROM (source)"));

        Assert.Contains("JOIN", exception.Message);
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

    [Fact]
    public void ParseStatement_Empty_Throws()
    {
        var lexer = new GenericLexer();
        var parser = new Parser();
        var tokens = lexer.Tokenize("");
        var exception = Assert.Throws<ParseException.ExpectedButFound>(() => parser.ParseStatement(new ParserState([.. tokens])));

        Assert.Contains("SQL statement", exception.Message);
    }

    [Fact]
    public void ParseStatement_KeywordCannotBeginStatement_Throws()
    {
        var lexer = new GenericLexer();
        var parser = new Parser();
        var tokens = lexer.Tokenize("ASC");
        var exception = Assert.Throws<ParseException.ExpectedButFound>(() => parser.ParseStatement(new ParserState([.. tokens])));

        Assert.Contains("SQL statement", exception.Message);
    }

    [Fact]
    public void Select_WithAs_IdentifierAsString_ConvertsToUnquoted()
    {
        var statements = ParseStatements("SELECT 1 AS 'alias'");
        var statement = Assert.Single(statements);

        var select = Assert.IsType<Select>(statement);
        var actualSql = select.ToSql();
        Assert.Equal("SELECT 1 AS alias", actualSql);
    }

    [Fact]
    public void Select_WithAs_NoIdentifier_Throws()
    {
        var lexer = new GenericLexer();
        var parser = new Parser();
        var tokens = lexer.Tokenize("SELECT 1 AS");
        var exception = Assert.Throws<ParseException.ExpectedButFound>(() => parser.Parse([.. tokens]));

        Assert.Contains("alias", exception.Message);
    }

    [Fact]
    public void CommonTableExpression_FollowingStatementInvalid_Throws()
    {
        var lexer = new GenericLexer();
        var parser = new Parser();
        var tokens = lexer.Tokenize("WITH cte AS (SELECT 1) ASC");
        var exception = Assert.Throws<ParseException.ExpectedOneOfButFound>(() => parser.ParseStatement(new ParserState([.. tokens])));

        Assert.Contains("{ SELECT | INSERT | UPDATE | DELETE }", exception.Message);
    }

    [Fact]
    public void CommonTableExpression_FollowingStatementEnclosed()
    {
        var lexer = new GenericLexer();
        var parser = new Parser();
        var tokens = lexer.Tokenize("WITH cte AS (SELECT 1) (SELECT 2)");
        var statements = parser.Parse([.. tokens]);
        var statement = Assert.Single(statements);

        var select = Assert.IsType<Select>(statement);
        var actualSql = select.ToSql();
        Assert.Equal("WITH cte AS (SELECT 1) (SELECT 2)", actualSql);
    }

    [Theory]
    [InlineData("BEGIN SELECT 1;")]
    [InlineData("SELECT 1 SELECT 2")]
    [InlineData("+")]
    [InlineData("UNKNOWN")]
    [InlineData("WITH mycte AS (SELECT 1) SET myvar = 1")]
    [InlineData("ALTER VIEW myview")]
    public void InvalidStatement_ThrowsParseException(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => new Parser().Parse(GetTokens(sql)));
    }
}
