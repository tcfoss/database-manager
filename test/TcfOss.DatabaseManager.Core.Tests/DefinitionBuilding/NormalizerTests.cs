using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Tests.Configuration;

namespace TcfOss.DatabaseManager.Core.Tests.DefinitionBuilding;

public class NormalizerTests
{
    private readonly PseudoTableSet _selectableSources;

    public NormalizerTests()
    {
        var catalogId = new CatalogIdentifier("def", QuoteStyle.Backticks);
        var catalogSchemaId = new SchemaIdentifier("library_catalog", catalogId, QuoteStyle.Backticks);
        var activitySchemaId = new SchemaIdentifier("library_activity", catalogId, QuoteStyle.Backticks);

        _selectableSources = new PseudoTableSet(catalogSchemaId, [
            new PseudoTable(
                "book",
                new ObjectIdentifier("book", catalogSchemaId),
                ["book_id", "full_title", "description", "publication_year", "isbn", "publisher"],
                PseudoTableType.Table
            ),
            new PseudoTable(
                "active_rental",
                new ObjectIdentifier("active_rental", activitySchemaId),
                ["rental_id", "book_id", "patron_id", "rental_date", "return_date", "really_active"],
                PseudoTableType.Table
            ),
            new PseudoTable(
                "book_author",
                new ObjectIdentifier("book_author", catalogSchemaId),
                ["book_id", "contributor_id", "display_order"],
                PseudoTableType.Table
            ),
            new PseudoTable(
                "contributor",
                new ObjectIdentifier("contributor", catalogSchemaId),
                ["contributor_id", "full_name", "description"],
                PseudoTableType.Table
            ),
        ]);
    }

    private string GetActualSelectText(
        string sqlText,
        IdentifierQuotationHandling? identifierQuotationHandling = null,
        IdentifierQuotationHandling? cteDeclarationNameQuotationHandling = null,
        bool usePseudoTables = true)
    {
        return GetNormalizedSelect(sqlText, identifierQuotationHandling, cteDeclarationNameQuotationHandling, usePseudoTables).ToSql();
    }

    private Select GetNormalizedSelect(
        string sqlText,
        IdentifierQuotationHandling? identifierQuotationHandling = null,
        IdentifierQuotationHandling? cteDeclarationNameQuotationHandling = null,
        bool usePseudoTables = true)
    {
        var textParser = new TextParser(new GenericLexer(), new Parser());
        Select statement = (Select)textParser.ParseText(sqlText).First();
        return GetNormalizedSelect(statement, identifierQuotationHandling, cteDeclarationNameQuotationHandling, usePseudoTables);
    }

    private Select GetNormalizedSelect(
        Select statement,
        IdentifierQuotationHandling? identifierQuotationHandling = null,
        IdentifierQuotationHandling? cteDeclarationNameQuotationHandling = null,
        bool usePseudoTables = true)
    {
        var normalizationSettings = new Core.Configuration.Parsing.NormalizationSettings
        {
            IdentifierQuotationHandling = identifierQuotationHandling ?? IdentifierQuotationHandling.Always,
            CteDeclarationNameQuotationHandling = cteDeclarationNameQuotationHandling ?? IdentifierQuotationHandling.Always,
        };

        var config = new GenericConfigLoader(new LoggerFactory().CreateLogger<GenericConfigLoader>()).LoadConfig("database", TestDataRawConfig.GetMyTestRawConfigLibrarySchemas(normalizationSettings), []);
        var name = new ObjectIdentifier("MyView", new SchemaIdentifier("my_schema", new CatalogIdentifier("my_catalog")));
        var componentNormalizer = new ComponentNormalizer(config.QuoteStyle, new FunctionNameProvider());
        var normalizer = new Normalizer(config, componentNormalizer);

        var normalizedSelect = normalizer.NormalizeSelect(statement, config.Schemas.Keys.First(), usePseudoTables ? _selectableSources : null, name);

        return normalizedSelect;
    }

    private static Identifier GetNormalizedIdentifier(
        string sqlText,
        QuoteStyle quoteStyle,
        IdentifierQuotationHandling? identifierQuotationHandling)
    {
        var lexer = new GenericLexer();
        var parser = new Parser();
        var parserState = new ParserState([.. lexer.Tokenize(sqlText)]);

        var identifier = parser.ComponentParser.ParseIdentifier(parserState);

        var componentNormalizer = new ComponentNormalizer(quoteStyle, new FunctionNameProvider());

        if (identifierQuotationHandling != null)
        {
            return componentNormalizer.NormalizeSingleIdentifier(identifier, identifierQuotationHandling.Value);
        }
        return componentNormalizer.NormalizeSingleIdentifier(identifier);
    }

    [Fact]
    public void SingleTable_Plain()
    {
        var text = """
        SELECT
            book_id,
            full_title
        FROM book;
        """;

        var actual = GetActualSelectText(text);
        var expected = "SELECT `library_catalog`.`book`.`book_id` AS `book_id`, `library_catalog`.`book`.`full_title` AS `full_title` FROM `library_catalog`.`book`";
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SingleTable_Aliases()
    {
        var text = """
        SELECT
            b.book_id,
            b.full_title
        FROM book AS b;
        """;

        var actual = GetActualSelectText(text);
        var expected = "SELECT `b`.`book_id` AS `book_id`, `b`.`full_title` AS `full_title` FROM `library_catalog`.`book` AS `b`";
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SingleTable_UnqualifiedColumnsWithAlias()
    {
        var text = """
        SELECT
            book_id,
            full_title
        FROM book AS b;
        """;

        var actual = GetActualSelectText(text);
        var expected = "SELECT `b`.`book_id` AS `book_id`, `b`.`full_title` AS `full_title` FROM `library_catalog`.`book` AS `b`";
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SingleTable_ManualColumnAlias()
    {
        var text = """
        SELECT
            book.book_id AS id,
            full_title
        FROM book;
        """;

        var actual = GetActualSelectText(text);
        var expected = "SELECT `library_catalog`.`book`.`book_id` AS `id`, `library_catalog`.`book`.`full_title` AS `full_title` FROM `library_catalog`.`book`";
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ThreeTableSingleSchema()
    {
        var text = """
        SELECT
            b.book_id,
            b.full_title,
            c.full_name
        FROM book AS b
        JOIN book_author AS ba
            ON b.book_id = ba.book_id
        JOIN contributor AS c
            ON ba.contributor_id = c.contributor_id;
        """;

        var actual = GetActualSelectText(text);
        var expected = "SELECT `b`.`book_id` AS `book_id`, `b`.`full_title` AS `full_title`, `c`.`full_name` AS `full_name` FROM `library_catalog`.`book` AS `b` INNER JOIN `library_catalog`.`book_author` AS `ba` ON `b`.`book_id` = `ba`.`book_id` INNER JOIN `library_catalog`.`contributor` AS `c` ON `ba`.`contributor_id` = `c`.`contributor_id`";
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ThreeTableSingleSchema_NoAliases()
    {
        var text = """
        SELECT
            book.book_id,
            full_title,
            full_name
        FROM book
        JOIN book_author
            ON book.book_id = book_author.book_id
        JOIN contributor
            ON book_author.contributor_id = contributor.contributor_id;
        """;

        var actual = GetActualSelectText(text);
        var expected = "SELECT `library_catalog`.`book`.`book_id` AS `book_id`, `library_catalog`.`book`.`full_title` AS `full_title`, `library_catalog`.`contributor`.`full_name` AS `full_name` FROM `library_catalog`.`book` INNER JOIN `library_catalog`.`book_author` ON `library_catalog`.`book`.`book_id` = `library_catalog`.`book_author`.`book_id` INNER JOIN `library_catalog`.`contributor` ON `library_catalog`.`book_author`.`contributor_id` = `library_catalog`.`contributor`.`contributor_id`";
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ThreeTableSingleSchema_Subquery()
    {
        var text = """
        SELECT
            b.book_id,
            b.full_title,
            b.description,
            full_name,
            auth.description AS author_description
        FROM book AS b
        JOIN (SELECT
                  ba.book_id,
                  full_name,
                  description
              FROM book_author AS ba
              JOIN (SELECT
                        contributor_id,
                        full_name,
                        description
                    FROM contributor
                    WHERE display_order < 10) AS contribs
              ON contribs.contributor_id = ba.contributor_id) AS auth
        ON b.book_id = auth.book_id;
        """;

        var actual = GetActualSelectText(text);
        var expected = "SELECT `b`.`book_id` AS `book_id`, `b`.`full_title` AS `full_title`, `b`.`description` AS `description`, `auth`.`full_name` AS `full_name`, `auth`.`description` AS `author_description` FROM `library_catalog`.`book` AS `b` INNER JOIN (SELECT `ba`.`book_id` AS `book_id`, `contribs`.`full_name` AS `full_name`, `contribs`.`description` AS `description` FROM `library_catalog`.`book_author` AS `ba` INNER JOIN (SELECT `library_catalog`.`contributor`.`contributor_id` AS `contributor_id`, `library_catalog`.`contributor`.`full_name` AS `full_name`, `library_catalog`.`contributor`.`description` AS `description` FROM `library_catalog`.`contributor` WHERE `ba`.`display_order` < 10) AS `contribs` ON `contribs`.`contributor_id` = `ba`.`contributor_id`) AS `auth` ON `b`.`book_id` = `auth`.`book_id`";
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TwoTableCrossSchema()
    {
        var text = """
        SELECT
            book.book_id,
            patron_id
        FROM book
        JOIN library_activity.active_rental
            ON book.book_id = active_rental.book_id;
        """;

        var actual = GetActualSelectText(text);
        var expected = "SELECT `library_catalog`.`book`.`book_id` AS `book_id`, `library_activity`.`active_rental`.`patron_id` AS `patron_id` FROM `library_catalog`.`book` INNER JOIN `library_activity`.`active_rental` ON `library_catalog`.`book`.`book_id` = `library_activity`.`active_rental`.`book_id`";
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Tables_With_Union()
    {
        var text = """
        (
            SELECT
                book_id,
                full_title
            FROM book
            WHERE publication_year > 2020
        )
        UNION
        (
            SELECT
                book_id,
                full_title
            FROM book
            WHERE publication_year < 1900
        )
        """;

        var actual = GetActualSelectText(text);
        var expected = "SELECT `library_catalog`.`book`.`book_id` AS `book_id`, `library_catalog`.`book`.`full_title` AS `full_title` FROM `library_catalog`.`book` WHERE `library_catalog`.`book`.`publication_year` > 2020 UNION SELECT `library_catalog`.`book`.`book_id` AS `book_id`, `library_catalog`.`book`.`full_title` AS `full_title` FROM `library_catalog`.`book` WHERE `library_catalog`.`book`.`publication_year` < 1900";
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Select_With_OrderByLimit()
    {
        var text = """
        SELECT
            book_id,
            full_title
        FROM book
        ORDER BY publication_year DESC, full_title ASC
        LIMIT 10;
        """;
        var actual = GetActualSelectText(text);
        var expected = "SELECT `library_catalog`.`book`.`book_id` AS `book_id`, `library_catalog`.`book`.`full_title` AS `full_title` FROM `library_catalog`.`book` ORDER BY `library_catalog`.`book`.`publication_year` DESC, `library_catalog`.`book`.`full_title` ASC LIMIT 10";
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SelectWithCte()
    {
        var text = """
        WITH RecentRentals AS (
            SELECT
                ar.book_id,
                ar.patron_id
            FROM library_activity.active_rental AS ar
            WHERE ar.rental_date > '2023-01-01'
        )
        SELECT
            book.book_id,
            patron_id
        FROM book
        JOIN RecentRentals
            ON book.book_id = RecentRentals.book_id;
        """;

        var actual = GetActualSelectText(text);
        var expected = "WITH `RecentRentals` AS (SELECT `ar`.`book_id` AS `book_id`, `ar`.`patron_id` AS `patron_id` FROM `library_activity`.`active_rental` AS `ar` WHERE `ar`.`rental_date` > '2023-01-01') SELECT `library_catalog`.`book`.`book_id` AS `book_id`, `RecentRentals`.`patron_id` AS `patron_id` FROM `library_catalog`.`book` INNER JOIN `RecentRentals` ON `library_catalog`.`book`.`book_id` = `RecentRentals`.`book_id`";
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SelectWithCte_ColumnsGiven()
    {
        var text = """
        WITH RecentRentals (book_id, patron_id) AS (
            SELECT
                ar.book_id,
                ar.patron_id
            FROM library_activity.active_rental AS ar
            WHERE ar.rental_date > '2023-01-01'
        )
        SELECT
            book.book_id,
            patron_id
        FROM book
        JOIN RecentRentals
            ON book.book_id = RecentRentals.book_id;
        """;

        var actual = GetActualSelectText(text);
        var expected = "WITH `RecentRentals` (`book_id`, `patron_id`) AS (SELECT `ar`.`book_id` AS `book_id`, `ar`.`patron_id` AS `patron_id` FROM `library_activity`.`active_rental` AS `ar` WHERE `ar`.`rental_date` > '2023-01-01') SELECT `library_catalog`.`book`.`book_id` AS `book_id`, `RecentRentals`.`patron_id` AS `patron_id` FROM `library_catalog`.`book` INNER JOIN `RecentRentals` ON `library_catalog`.`book`.`book_id` = `RecentRentals`.`book_id`";
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SelectWithCte_UnquoteCteName()
    {
        var text = """
        WITH `RecentRentals` AS (
            SELECT
                ar.book_id,
                ar.patron_id
            FROM library_activity.active_rental AS ar
            WHERE ar.rental_date > '2023-01-01'
        )
        SELECT
            book.book_id,
            patron_id
        FROM book
        JOIN RecentRentals
            ON book.book_id = RecentRentals.book_id;
        """;

        var actual = GetActualSelectText(text, cteDeclarationNameQuotationHandling: IdentifierQuotationHandling.OnlyIfSpecial);
        var expected = "WITH RecentRentals AS (SELECT `ar`.`book_id` AS `book_id`, `ar`.`patron_id` AS `patron_id` FROM `library_activity`.`active_rental` AS `ar` WHERE `ar`.`rental_date` > '2023-01-01') SELECT `library_catalog`.`book`.`book_id` AS `book_id`, `RecentRentals`.`patron_id` AS `patron_id` FROM `library_catalog`.`book` INNER JOIN `RecentRentals` ON `library_catalog`.`book`.`book_id` = `RecentRentals`.`book_id`";
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SelectWithFunctionCalls()
    {
        var text = """
        SELECT
            b.book_id,
            b.full_title,
            LENGTH(b.full_title) AS title_length,
            GROUP_CONCAT(full_name ORDER BY display_order LIMIT 5 SEPARATOR ', ') AS authors
        FROM book as b
        JOIN book_author AS ba
            ON b.book_id = ba.book_id
            JOIN contributor AS c;
        """;

        var actual = GetActualSelectText(text);
        var expected = "SELECT `b`.`book_id` AS `book_id`, `b`.`full_title` AS `full_title`, LENGTH(`b`.`full_title`) AS `title_length`, GROUP_CONCAT(`c`.`full_name` ORDER BY `ba`.`display_order` LIMIT 5 SEPARATOR ', ') AS `authors` FROM `library_catalog`.`book` AS `b` INNER JOIN `library_catalog`.`book_author` AS `ba` ON `b`.`book_id` = `ba`.`book_id` INNER JOIN `library_catalog`.`contributor` AS `c`";
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NestedJoin_IsFlattened()
    {
        // Simulates IS SQL from MariaDB which stores joins in nested form
        var text = "SELECT `b`.`book_id` AS `book_id`, `b`.`full_title` AS `full_title`, `c`.`full_name` AS `full_name` FROM ((`library_catalog`.`book` AS `b` INNER JOIN `library_catalog`.`book_author` AS `ba` ON `b`.`book_id` = `ba`.`book_id`) INNER JOIN `library_catalog`.`contributor` AS `c` ON `ba`.`contributor_id` = `c`.`contributor_id`)";

        var actual = GetActualSelectText(text);
        var expected = "SELECT `b`.`book_id` AS `book_id`, `b`.`full_title` AS `full_title`, `c`.`full_name` AS `full_name` FROM `library_catalog`.`book` AS `b` INNER JOIN `library_catalog`.`book_author` AS `ba` ON `b`.`book_id` = `ba`.`book_id` INNER JOIN `library_catalog`.`contributor` AS `c` ON `ba`.`contributor_id` = `c`.`contributor_id`";
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NestedBinaryOperator_IsStripped()
    {
        // Simulates IS SQL from MySQL which wraps binary operators in parentheses
        var text = "SELECT `b`.`book_id` AS `book_id` FROM `library_catalog`.`book` AS `b` INNER JOIN `library_catalog`.`book_author` AS `ba` ON ((`b`.`book_id` = `ba`.`book_id`) AND (`ba`.`display_order` > 1))";

        var actual = GetActualSelectText(text);
        var expected = "SELECT `b`.`book_id` AS `book_id` FROM `library_catalog`.`book` AS `b` INNER JOIN `library_catalog`.`book_author` AS `ba` ON `b`.`book_id` = `ba`.`book_id` AND `ba`.`display_order` > 1";
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NowFunction_IsConvertedToCurrentTimestamp()
    {
        var text = """
        SELECT
            book_id,
            NOW() AS queried_at
        FROM book
        WHERE NOW() > publication_year;
        """;

        var actual = GetActualSelectText(text);
        Assert.Contains("CURRENT_TIMESTAMP()", actual);
        Assert.DoesNotContain("NOW()", actual);
    }

    [Fact]
    public void ExistsWithLimit_LimitIsStripped()
    {
        var text = """
        SELECT
            book_id
        FROM book
        WHERE EXISTS (SELECT 1 FROM book_author WHERE book_author.book_id = book.book_id LIMIT 1);
        """;

        var actual = GetActualSelectText(text);
        Assert.DoesNotContain("LIMIT", actual);
    }

    [Fact]
    public void ExistsIsFalse_IsCanonicalizedToNotExists_AndLimitIsStripped()
    {
        var text = """
        SELECT
            book_id
        FROM book
        WHERE EXISTS (SELECT 1 FROM book_author WHERE book_author.book_id = book.book_id LIMIT 1) IS FALSE;
        """;

        var actual = GetActualSelectText(text);
        Assert.Contains("NOT EXISTS", actual);
        Assert.DoesNotContain("IS FALSE", actual);
        Assert.DoesNotContain("LIMIT", actual);
    }

    [Fact]
    public void NonExistsIsFalse_RemainsIsFalse()
    {
        var text = """
        SELECT
            book_id
        FROM book
        WHERE (publication_year > 2020) IS FALSE;
        """;

        var actual = GetActualSelectText(text);
        Assert.Contains("IS FALSE", actual);
    }

    [Fact]
    public void InSubquery_IsPreserved()
    {
        var text = """
        SELECT
            book_id
        FROM book
        WHERE book_id IN (SELECT book_id FROM book_author);
        """;

        var actual = GetActualSelectText(text);
        Assert.Contains(" IN (", actual);
        Assert.Contains("FROM book_author", actual);
    }

    [Fact]
    public void BetweenExpression_IsNormalized()
    {
        var text = """
        SELECT
            book_id
        FROM book
        WHERE publication_year BETWEEN 1900 AND 2020;
        """;

        var actual = GetActualSelectText(text);
        Assert.Contains("`library_catalog`.`book`.`publication_year` BETWEEN 1900 AND 2020", actual);
    }

    [Fact]
    public void CaseExpression_IsNormalized()
    {
        var text = """
        SELECT
            CASE
                WHEN publication_year > 2000 THEN full_title
                ELSE description
            END AS selected_text
        FROM book;
        """;

        var actual = GetActualSelectText(text);
        Assert.Contains("CASE WHEN `library_catalog`.`book`.`publication_year` > 2000 THEN `library_catalog`.`book`.`full_title` ELSE `library_catalog`.`book`.`description` END AS `selected_text`", actual);
    }

    [Fact]
    public void InListExpression_IsNormalized()
    {
        var text = """
        SELECT
            book_id
        FROM book
        WHERE publication_year IN (1900, 2000, 2020);
        """;

        var actual = GetActualSelectText(text);
        Assert.Contains("`library_catalog`.`book`.`publication_year` IN (1900, 2000, 2020)", actual);
    }

    [Fact]
    public void UnaryOperatorExpression_IsNormalized()
    {
        var text = """
        SELECT
            book_id
        FROM book
        WHERE NOT (publication_year > 2000);
        """;

        var actual = GetActualSelectText(text);
        Assert.Contains("NOT `library_catalog`.`book`.`publication_year` > 2000", actual);
    }

    private static string GetNormalizedExpressionText(string sqlText, bool flatten)
    {
        var config = new GenericConfigLoader(new LoggerFactory().CreateLogger<GenericConfigLoader>()).LoadConfig("database", TestDataRawConfig.GetMyTestRawConfigLibrarySchemas(new Core.Configuration.Parsing.NormalizationSettings()), []);
        var parser = new Parser();
        var expression = parser.ExpressionParser.ParseExpr(new ParserState([.. new GenericLexer().Tokenize(sqlText)]));
        var normalizer = new Normalizer(config, new ComponentNormalizer(config.QuoteStyle, new FunctionNameProvider()));
        var normalized = flatten ? normalizer.NormalizeExpression(expression) : normalizer.NormalizeExpressionNoFlatten(expression);
        return normalized.ToSql();
    }

    [Fact]
    public void NormalizeExpression_Flatten_StripsNesting()
    {
        var actual = GetNormalizedExpressionText("(a + b) IS NULL", flatten: true);
        Assert.DoesNotContain("(", actual);
        Assert.Contains("IS NULL", actual);
    }

    [Fact]
    public void NormalizeExpression_NoFlatten_KeepsNesting()
    {
        var actual = GetNormalizedExpressionText("(a + b) IS NULL", flatten: false);
        Assert.Contains("(", actual);
    }

    [Fact]
    public void NormalizeExpression_CompoundIdentifier_WithoutPseudoTables()
    {
        var actual = GetNormalizedExpressionText("t.col = col2", flatten: true);
        Assert.Contains("t", actual);
        Assert.Contains("col2", actual);
    }

    [Fact]
    public void IsNullExpression_IsNormalized()
    {
        var text = "SELECT book_id FROM book WHERE description IS NOT NULL;";

        var actual = GetActualSelectText(text);
        Assert.Contains("`library_catalog`.`book`.`description` IS NOT NULL", actual);
    }

    [Fact]
    public void CastExpression_IsNormalized()
    {
        var text = "SELECT CAST(publication_year AS CHAR(10)) AS year_text FROM book;";

        var actual = GetActualSelectText(text);
        Assert.Contains("CAST(`library_catalog`.`book`.`publication_year` AS CHAR(10))", actual);
    }

    [Fact]
    public void CountQualifiedWildcard_IsNormalized()
    {
        var text = "SELECT COUNT(b.*) AS cnt FROM book AS b;";

        var actual = GetActualSelectText(text);
        Assert.Contains("COUNT(`b`.*)", actual);
    }

    [Fact]
    public void CountWildcard_IsPreserved()
    {
        var text = "SELECT COUNT(*) AS cnt FROM book;";

        var actual = GetActualSelectText(text);
        Assert.Contains("COUNT(*)", actual);
    }

    [Fact]
    public void NamedFunctionArgument_IsNormalized()
    {
        var text = "SELECT FOO(name => publication_year) AS val FROM book;";

        var actual = GetActualSelectText(text);
        Assert.Contains("`library_catalog`.`book`.`publication_year`", actual);
        Assert.Contains("=>", actual);
    }

    [Theory]
    [InlineData("RIGHT JOIN")]
    [InlineData("FULL OUTER JOIN")]
    [InlineData("LEFT JOIN")]
    public void OuterJoins_AreNormalized(string joinText)
    {
        var text = $"SELECT b.book_id AS book_id FROM book AS b {joinText} book_author AS ba ON b.book_id = ba.book_id;";

        var actual = GetActualSelectText(text);
        Assert.Contains("`library_catalog`.`book_author` AS `ba` ON `b`.`book_id` = `ba`.`book_id`", actual);
    }

    [Fact]
    public void JoinUsing_IsNotSupported()
    {
        var text = "SELECT b.book_id AS book_id FROM book AS b JOIN book_author AS ba USING (book_id);";

        Assert.Throws<NotImplementedException>(() => GetActualSelectText(text));
    }

    [Fact]
    public void ValuesQuery_IsNotSupported()
    {
        Assert.Throws<NotImplementedException>(() => GetActualSelectText("VALUES (1, 2)"));
    }

    [Theory]
    [InlineData("SELECT * FROM book")]
    [InlineData("SELECT b.* FROM book AS b")]
    public void SelectWildcard_Throws(string text)
    {
        Assert.Throws<Errors.DefinitionException.ViewSelectWildcard>(() => GetActualSelectText(text));
    }

    [Fact]
    public void SelectExpressionWithoutAlias_Throws()
    {
        Assert.Throws<Errors.DefinitionException.ViewSelectWithoutAlias>(() => GetActualSelectText("SELECT 1 + 1 FROM book"));
    }

    [Fact]
    public void DerivedTable_IsNormalized()
    {
        var text = "SELECT d.book_id AS book_id FROM (SELECT book_id FROM book) AS d;";

        var actual = GetActualSelectText(text);
        Assert.Contains("FROM (SELECT `library_catalog`.`book`.`book_id` AS `book_id` FROM `library_catalog`.`book`) AS `d`", actual);
    }

    [Fact]
    public void GroupByAndHaving_AreNormalized()
    {
        var text = "SELECT publisher AS publisher, COUNT(book_id) AS cnt FROM book GROUP BY publisher HAVING COUNT(book_id) > 1;";

        var actual = GetActualSelectText(text);
        Assert.Contains("GROUP BY `library_catalog`.`book`.`publisher` HAVING COUNT(`library_catalog`.`book`.`book_id`) > 1", actual);
    }

    [Fact]
    public void LimitOffset_IsNormalized()
    {
        var actual = GetActualSelectText("SELECT book_id FROM book ORDER BY book_id LIMIT 5 OFFSET 10");
        Assert.Contains("ORDER BY `library_catalog`.`book`.`book_id` LIMIT 5 OFFSET 10", actual);
    }

    [Fact]
    public void LimitCommaSeparated_IsNormalized()
    {
        var actual = GetActualSelectText("SELECT book_id FROM book ORDER BY book_id LIMIT 10, 5");
        Assert.Contains("SELECT `library_catalog`.`book`.`book_id`", actual);
        Assert.Contains("ORDER BY `library_catalog`.`book`.`book_id` LIMIT 10, 5", actual);
    }

    [Fact]
    public void ParenthesizedSelectWithOrderByLimit_IsNormalized()
    {
        var actual = GetActualSelectText("(SELECT book_id FROM book) ORDER BY 1 LIMIT 5");
        Assert.DoesNotContain("(", actual);
        Assert.Contains("SELECT `library_catalog`.`book`.`book_id`", actual);
        Assert.Contains("ORDER BY", actual);
        Assert.Contains("LIMIT 5", actual);
    }

    [Fact]
    public void ParenthesizedSetOperation_IsDenestedByFlatteningNormalization()
    {
        Select normalized = GetNormalizedSelect("(SELECT book_id FROM book UNION SELECT book_id FROM book_author) INTERSECT SELECT book_id FROM book");
        SelectBody.SetOperation outer = Assert.IsType<SelectBody.SetOperation>(normalized.Body);
        Assert.IsType<SelectBody.SetOperation>(outer.Left);
    }

    [Fact]
    public void ParenthesizedSetOperation_RemainsNestedInsideDerivedTable()
    {
        Select normalized = GetNormalizedSelect("SELECT d.book_id FROM ((SELECT book_id FROM book UNION SELECT book_id FROM book_author) INTERSECT SELECT book_id FROM book) AS d");
        SelectBody.SimpleSelectQuery selectBody = Assert.IsType<SelectBody.SimpleSelectQuery>(normalized.Body);
        TableWithJoins from = Assert.Single(selectBody.Query.From!);
        TableFactor.Derived derived = Assert.IsType<TableFactor.Derived>(from.Relation);
        SelectBody.SetOperation outer = Assert.IsType<SelectBody.SetOperation>(derived.SubQuery.Body);
        SelectBody.Nested nested = Assert.IsType<SelectBody.Nested>(outer.Left);
        Assert.IsType<SelectBody.SetOperation>(nested.Query.Body);
    }

    [Fact]
    public void ParenthesizedSelectWithInnerClauses_RemainsNestedWhenFlattening()
    {
        Select normalized = GetNormalizedSelect("(SELECT book_id FROM book ORDER BY book_id LIMIT 5)");
        SelectBody.Nested nested = Assert.IsType<SelectBody.Nested>(normalized.Body);
        Assert.NotNull(nested.Query.OrderBy);
        Assert.NotNull(nested.Query.Limit);
    }

    [Fact]
    public void ParenthesizedSelectOrderByColumn_UsesOutputColumnName()
    {
        var actual = GetActualSelectText("(SELECT book_id FROM book) ORDER BY book_id");
        Assert.Contains("ORDER BY `book_id`", actual);
    }

    [Fact]
    public void UnionOrderByColumn_UsesOutputColumnName()
    {
        var actual = GetActualSelectText("(SELECT book_id FROM book) UNION (SELECT book_id FROM book_author) ORDER BY book_id");
        Assert.Contains("ORDER BY `book_id`", actual);
    }

    [Fact]
    public void NoPseudoTables_IdentifiersAreOnlyQuoted()
    {
        var actual = GetActualSelectText("SELECT book_id, b.full_title FROM book AS b", usePseudoTables: false);
        Assert.Equal("SELECT `book_id` AS `book_id`, `b`.`full_title` AS `full_title` FROM `book` AS `b`", actual);
    }

    [Fact]
    public void AliasedNestedJoin_IsPreserved()
    {
        var actual = GetActualSelectText("SELECT n.book_id AS book_id FROM (book AS b JOIN book_author AS ba ON b.book_id = ba.book_id) AS n", usePseudoTables: false);
        Assert.Contains(") AS `n`", actual);
    }

    [Fact]
    public void InnerSelectQueryOrderByAndLimit_AreNormalized()
    {
        var actual = GetActualSelectText("(SELECT book_id FROM book ORDER BY book_id LIMIT 5)");
        Assert.Contains("ORDER BY `library_catalog`.`book`.`book_id`", actual);
        Assert.Contains("LIMIT 5", actual);
    }

    [Fact]
    public void DerivedTableOrderBy_ResolvesAgainstItsOwnSources()
    {
        var actual = GetActualSelectText("SELECT d.book_id AS book_id FROM (SELECT book_id FROM book ORDER BY full_title LIMIT 5) AS d");
        Assert.Contains("ORDER BY `library_catalog`.`book`.`full_title` LIMIT 5) AS `d`", actual);
    }

    [Fact]
    public void CrossJoin_IsPreserved()
    {
        var actual = GetActualSelectText("SELECT b.book_id AS book_id FROM book AS b CROSS JOIN book_author AS ba");
        Assert.Contains("CROSS JOIN `library_catalog`.`book_author` AS `ba`", actual);
    }

    [Fact]
    public void SelectWithoutFrom_IsNormalized()
    {
        var actual = GetActualSelectText("SELECT 1 AS x");
        Assert.Equal("SELECT 1 AS `x`", actual);
    }

    [Theory]
    [InlineData("simple_name", QuoteStyle.Ansi, IdentifierQuotationHandling.Always, "\"simple_name\"")]
    [InlineData("@session_var", QuoteStyle.Backticks, IdentifierQuotationHandling.Always, "@session_var")]
    [InlineData("`has-dash`", QuoteStyle.Backticks, IdentifierQuotationHandling.IfSpecial, "`has-dash`")]
    [InlineData("simple_name", QuoteStyle.Backticks, IdentifierQuotationHandling.IfSpecial, "simple_name")]
    [InlineData("`has-dash`", QuoteStyle.Backticks, IdentifierQuotationHandling.OnlyIfSpecial, "`has-dash`")]
    [InlineData("`simple_name`", QuoteStyle.Backticks, IdentifierQuotationHandling.OnlyIfSpecial, "simple_name")]
    [InlineData("`NOW`", QuoteStyle.Backticks, IdentifierQuotationHandling.IfSpecialOrFunction, "`NOW`")]
    [InlineData("simple_name", QuoteStyle.Backticks, IdentifierQuotationHandling.IfSpecialOrFunction, "simple_name")]
    [InlineData("`NOW`", QuoteStyle.Backticks, IdentifierQuotationHandling.OnlyIfSpecialOrFunction, "`NOW`")]
    [InlineData("`simple_name`", QuoteStyle.Backticks, IdentifierQuotationHandling.OnlyIfSpecialOrFunction, "simple_name")]
    [InlineData("`original_name`", QuoteStyle.Ansi, IdentifierQuotationHandling.LeaveOriginal, "`original_name`")]
    public void NormalizeSingleIdentifier_IdentifierQuotationHandling(string givenText, QuoteStyle quoteStyle, IdentifierQuotationHandling identifierQuotationHandling, string expectedIdentifier)
    {
        var actual = GetNormalizedIdentifier(givenText, quoteStyle, identifierQuotationHandling);
        Assert.Equal(expectedIdentifier, actual.ToSql());
    }
}
