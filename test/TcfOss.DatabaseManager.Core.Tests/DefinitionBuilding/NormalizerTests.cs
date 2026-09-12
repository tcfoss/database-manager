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
        IdentifierQuotationHandling? cteDeclarationNameQuotationHandling = null)
    {
        var normalizationSettings = new Core.Configuration.Parsing.NormalizationSettings
        {
            IdentifierQuotationHandling = identifierQuotationHandling ?? IdentifierQuotationHandling.Always,
            CteDeclarationNameQuotationHandling = cteDeclarationNameQuotationHandling ?? IdentifierQuotationHandling.Always,
        };

        var config = new ConfigLoader(new LoggerFactory().CreateLogger<ConfigLoader>()).LoadConfig("database", TestDataRawConfig.GetMyTestRawConfigLibrarySchemas(normalizationSettings), []);
        var textParser = new TextParser(new GenericLexer(), new Parser());
        var statement = (Select)textParser.ParseText(sqlText).First();

        var name = new ObjectIdentifier("MyView", new SchemaIdentifier("my_schema", new CatalogIdentifier("my_catalog")));
        var componentNormalizer = new ComponentNormalizer(config.QuoteStyle, new FunctionNameProvider());
        var normalizer = new Normalizer(config, componentNormalizer);

        var normalizedSelect = normalizer.NormalizeSelect(statement, config.Schemas.Keys.First(), _selectableSources, name);

        return normalizedSelect.ToSql();
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
