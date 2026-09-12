using TcfOss.DatabaseManager.Core.Configuration.Attributes;

namespace TcfOss.DatabaseManager.MySql.Tests.IO.Formatting;

public class FormatSelectTests
{
    [Fact]
    public void Select_Simple_QuoteAll_NoPseudoTables()
    {
        var text = "SELECT title, author_id  , name FROM books JOIN authors a ON books.author_id= a.id WHERE published_year>2000 ORDER BY title, published_year";
        var formatted = """
        SELECT
            `title`,
            `author_id`,
            `name`
        FROM `books`
        INNER JOIN `authors` AS `a` ON `books`.`author_id` = `a`.`id`
        WHERE `published_year` > 2000
        ORDER BY `title`, `published_year`;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Select_JoinUsing_NoPseudoTables()
    {
        var text = "SELECT a.*, b.* FROM table1 AS a INNER JOIN table2 AS b USING(id, name)";
        var formatted = """
        SELECT
            `a`.*,
            `b`.*
        FROM `table1` AS `a`
        INNER JOIN `table2` AS `b` USING (`id`, `name`);
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Select_JoinUsing_IndentUsing()
    {
        var text = "SELECT a.*, b.* FROM table1 AS a INNER JOIN table2 AS b USING(id, name)";
        var formatted = """
        SELECT
            `a`.*,
            `b`.*
        FROM `schema1`.`table1` AS `a`
        INNER JOIN `schema1`.`table2` AS `b`
            USING (`id`, `name`);
        """;

        var (config, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables3);
        config.Formatting.JoinConditionIndent = 4;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Select_NaturalJoin()
    {
        var text = "SELECT     DISTINCT  *, b.* FROM table1 AS a NATURAL JOIN table2 AS b";
        var formatted = """
        SELECT DISTINCT
            *,
            `b`.*
        FROM `table1` AS `a`
        NATURAL INNER JOIN `table2` AS `b`;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Select_CrossJoin()
    {
        var text = "SELECT     DISTINCT  * FROM table1 AS a CROSS JOIN table2 AS b";
        var formatted = """
        SELECT DISTINCT
            *
        FROM `table1` AS `a`
        CROSS JOIN `table2` AS `b`;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Select_Simple_IndentJoin_AlwaysQuote_PrefixObject_PrefixName()
    {
        var text = "SELECT title, author_id  , name FROM books JOIN authors a ON books.author_id= a.id WHERE published_year>2000 ORDER BY title, published_year desc";
        var formatted = """
        SELECT
            `books`.`title`,
            `books`.`author_id`,
            `a`.`name`
        FROM `schema1`.`books`
        INNER JOIN `schema1`.`authors` AS `a`
            ON `books`.`author_id` = `a`.`id`
        WHERE `books`.`published_year` > 2000
        ORDER BY `books`.`title`, `books`.`published_year` DESC;
        """;

        var (config, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
        config.Formatting.Quoting = IdentifierQuotationHandling.Always;
        config.Formatting.ObjectNamePrefixWithSchema = true;
        config.Formatting.SelectItemPrefixWithObject = true;
        config.Formatting.JoinConditionIndent = 4;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Select_Simple_IndentJoin_AlwaysQuote_NoPrefixObject_PrefixName()
    {
        var text = "SELECT title, author_id  , name FROM books JOIN authors a ON books.author_id= a.id WHERE published_year>2000 ORDER BY title, published_year";
        var formatted = """
        SELECT
            `books`.`title`,
            `books`.`author_id`,
            `a`.`name`
        FROM `books`
        INNER JOIN `authors` AS `a`
            ON `books`.`author_id` = `a`.`id`
        WHERE `books`.`published_year` > 2000
        ORDER BY `books`.`title`, `books`.`published_year`;
        """;

        var (config, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
        config.Formatting.Quoting = IdentifierQuotationHandling.Always;
        config.Formatting.ObjectNamePrefixWithSchema = false;
        config.Formatting.SelectItemPrefixWithObject = true;
        config.Formatting.JoinConditionIndent = 4;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Select_Simple_NullIndentJoin_AlwaysQuote_NoPrefixObject_PrefixName()
    {

        var text = "SELECT title, author_id  , name FROM books JOIN authors a ON books.author_id= a.id WHERE published_year>2000 ORDER BY title, published_year";
        var formatted = """
        SELECT
            `books`.`title`,
            `books`.`author_id`,
            `a`.`name`
        FROM `books`
        INNER JOIN `authors` AS `a` ON `books`.`author_id` = `a`.`id`
        WHERE `books`.`published_year` > 2000
        ORDER BY `books`.`title`, `books`.`published_year`;
        """;

        var (config, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
        config.Formatting.Quoting = IdentifierQuotationHandling.Always;
        config.Formatting.ObjectNamePrefixWithSchema = false;
        config.Formatting.SelectItemPrefixWithObject = true;
        config.Formatting.JoinConditionIndent = null;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Select_Simple_ZeroIndentJoin_AlwaysQuote_NoPrefixObject_PrefixName()
    {
        var text = "SELECT title, author_id  , name FROM books JOIN authors a ON books.author_id= a.id WHERE published_year>2000 ORDER BY title, published_year";
        var formatted = """
        SELECT
            `books`.`title`,
            `books`.`author_id`,
            `a`.`name`
        FROM `books`
        INNER JOIN `authors` AS `a`
        ON `books`.`author_id` = `a`.`id`
        WHERE `books`.`published_year` > 2000
        ORDER BY `books`.`title`, `books`.`published_year`;
        """;

        var (config, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
        config.Formatting.Quoting = IdentifierQuotationHandling.Always;
        config.Formatting.ObjectNamePrefixWithSchema = false;
        config.Formatting.SelectItemPrefixWithObject = true;
        config.Formatting.JoinConditionIndent = 0;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Select_NestedJoin()
    {
        var text = "SELECT a.title, a.published_year, c.first_name AS author_name FROM ((books AS a join book_authors AS b ON (a.book_id = b.book_id)) JOIN contributors as c on (b.contributor_id = c.contributor_id))";
        var formatted = """
        SELECT
            `a`.`title`,
            `a`.`published_year`,
            `c`.`first_name` AS author_name
        FROM (
            (
                `books` AS `a`
                INNER JOIN `book_authors` AS `b` ON (`a`.`book_id` = `b`.`book_id`)
            )
            INNER JOIN `contributors` AS `c` ON (`b`.`contributor_id` = `c`.`contributor_id`)
        );
        """;

        var (config, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables2);
        config.Formatting.ObjectNamePrefixWithSchema = false;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Select_NestedJoin_IndentOn()
    {
        var text = "SELECT a.title, a.published_year, c.first_name AS author_name FROM ((books AS a join book_authors AS b ON (a.book_id = b.book_id)) JOIN contributors as c on (b.contributor_id = c.contributor_id))";
        var formatted = """
        SELECT
            `a`.`title`,
            `a`.`published_year`,
            `c`.`first_name` AS author_name
        FROM (
            (
                `books` AS `a`
                INNER JOIN `book_authors` AS `b`
                    ON (`a`.`book_id` = `b`.`book_id`)
            )
            INNER JOIN `contributors` AS `c`
                ON (`b`.`contributor_id` = `c`.`contributor_id`)
        );
        """;

        var (config, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables2);
        config.Formatting.ObjectNamePrefixWithSchema = false;
        config.Formatting.JoinConditionIndent = 4;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Select_WithSubqueries()
    {
        var text = """
        SELECT b.title,
        published_year,
                auth.first_name AS author_name
        FROM books b
        JOIN (SELECT auth.book_id,
                    contrib.author_id AS author_id,
                    contrib.first_name,
                    contrib.last_name
            FROM book_authors ba
            INNER JOIN (SELECT contributor_id AS author_id, first_name, last_name, description
                        FROM contributors
                        WHERE contributor_id = ba.contributor_id
                        AND   list_order <= 10
                        ) AS contrib
                ON ba.contributor_id = contrib.author_id
            ) AS auth
            ON b.book_id = auth.book_id
        WHERE b.published_year > 2000
        ORDER BY b.title;
        """;

        var formatted = """
        SELECT
            `b`.`title`,
            `b`.`published_year`,
            `auth`.`first_name` AS author_name
        FROM `books` AS `b`
        INNER JOIN (
            SELECT
                `auth`.`book_id`,
                `contrib`.`author_id` AS author_id,
                `contrib`.`first_name`,
                `contrib`.`last_name`
            FROM `book_authors` AS `ba`
            INNER JOIN (
                SELECT
                    `contributors`.`contributor_id` AS author_id,
                    `contributors`.`first_name`,
                    `contributors`.`last_name`,
                    `contributors`.`description`
                FROM `contributors`
                WHERE `contributors`.`contributor_id` = `ba`.`contributor_id` AND `ba`.`list_order` <= 10) AS contrib
                ON `ba`.`contributor_id` = `contrib`.`author_id`) AS auth
            ON `b`.`book_id` = `auth`.`book_id`
        WHERE `b`.`published_year` > 2000
        ORDER BY `b`.`title`;
        """;

        var (config, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables2);
        config.Formatting.ObjectNamePrefixWithSchema = false;
        config.Formatting.JoinConditionIndent = 4;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Select_SubqueryInSelectList()
    {

        var text = """
        SELECT b.title,
               published_year,
               (SELECT c.first_name
               FROM book_authors ba
               JOIN contributors c
               ON ba.author_id = c.contributor_id
               WHERE ba.book_id = b.book_id) AS author_first_name
        FROM books b
        WHERE b.published_year > 2000
        """;

        var formatted = """
        SELECT
            `b`.`title`,
            `b`.`published_year`,
            (
                SELECT
                    `c`.`first_name`
                FROM `book_authors` AS `ba`
                INNER JOIN `contributors` AS `c`
                    ON `ba`.`author_id` = `c`.`contributor_id`
                WHERE `ba`.`book_id` = `b`.`book_id`) AS author_first_name
        FROM `books` AS `b`
        WHERE `b`.`published_year` > 2000;
        """;

        var (config, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables2);
        config.Formatting.ObjectNamePrefixWithSchema = false;
        config.Formatting.JoinConditionIndent = 4;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Select_WithCTEs()
    {

        var text = """
        WITH authors AS(
                SELECT ba.book_id,  c.last_name,    c.first_name,description
                FROM book_authors AS ba JOIN contributors aS c   ON ba.contributor_id = c.contributor_id
                WHERE list_order <= 10
            )
            SELECT b.title,               first_name,last_name,a.description AS author_description
            FROM books AS b
                    JOIN authors AS a
        ON b.book_id = a.book_id
        WHERE b.published_year >= 2010
        """;

        var formatted = """
        WITH `authors` AS (
            SELECT
                `ba`.`book_id`,
                `c`.`last_name`,
                `c`.`first_name`,
                `c`.`description`
            FROM `book_authors` AS `ba`
            INNER JOIN `contributors` AS `c`
                ON `ba`.`contributor_id` = `c`.`contributor_id`
            WHERE `ba`.`list_order` <= 10)
        SELECT
            `b`.`title`,
            `a`.`first_name`,
            `a`.`last_name`,
            `a`.`description` AS author_description
        FROM `books` AS `b`
        INNER JOIN `authors` AS `a`
            ON `b`.`book_id` = `a`.`book_id`
        WHERE `b`.`published_year` >= 2010;
        """;

        var (config, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables2);
        config.Formatting.ObjectNamePrefixWithSchema = false;
        config.Formatting.JoinConditionIndent = 4;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Select_Union()
    {

        var text = """
        SELECT b.title, b.published_year
        FROM books b
        WHERE b.published_year > 2000 UNION
        SELECT b.title, b.published_year
        FROM books b
        WHERE b.published_year <= 2000 ORDER BY published_year;
        """;

        var formatted = """
        SELECT
            `b`.`title`,
            `b`.`published_year`
        FROM `books` AS `b`
        WHERE `b`.`published_year` > 2000
        UNION
        SELECT
            `b`.`title`,
            `b`.`published_year`
        FROM `books` AS `b`
        WHERE `b`.`published_year` <= 2000
        ORDER BY `b`.`published_year`;
        """;

        var (config, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables2);
        config.Formatting.ObjectNamePrefixWithSchema = false;
        config.Formatting.JoinConditionIndent = 4;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Select_UnionInCte()
    {

        var text = """
        WITH recent_books AS (
            SELECT b.title,
                   b.published_year
            FROM books b WHERE b.published_year > 2000 UNION ALL
            SELECT b.title,
                   b.published_year
            FROM books b    WHERE b.year_added > 2020
        )
        SELECT b.title,b.published_year
        FROM recent_books b
        ORDER BY b.title;
        """;

        var formatted = """
        WITH `recent_books` AS (
            SELECT
                `b`.`title`,
                `b`.`published_year`
            FROM `books` AS `b`
            WHERE `b`.`published_year` > 2000
            UNION ALL
            SELECT
                `b`.`title`,
                `b`.`published_year`
            FROM `books` AS `b`
            WHERE `b`.`year_added` > 2020)
        SELECT
            `b`.`title`,
            `b`.`published_year`
        FROM `recent_books` AS `b`
        ORDER BY `b`.`title`;
        """;

        var (config, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables2);
        config.Formatting.ObjectNamePrefixWithSchema = false;
        config.Formatting.JoinConditionIndent = 4;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Select_GroupByHaving()
    {

        var text = """
        SELECT b.title, COUNT(*) AS num_copies
        FROM books AS b GROUP BY b.title HAVING COUNT(*) > 1
        """;

        var formatted = """
        SELECT
            `b`.`title`,
            COUNT(*) AS num_copies
        FROM `books` AS `b`
        GROUP BY `b`.`title`
        HAVING COUNT(*) > 1;
        """;

        var (config, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables2);
        config.Formatting.ObjectNamePrefixWithSchema = false;
        config.Formatting.JoinConditionIndent = 4;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Select_QualifiedCount()
    {

        var text = """
        SELECT b.title, COUNT(b.*) AS num_copies
        FROM books AS b GROUP BY b.title HAVING COUNT(b.*) > 1
        """;

        var formatted = """
        SELECT
            `b`.`title`,
            COUNT(`b`.*) AS num_copies
        FROM `books` AS `b`
        GROUP BY `b`.`title`
        HAVING COUNT(`b`.*) > 1;
        """;

        var (config, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables2);
        config.Formatting.ObjectNamePrefixWithSchema = false;
        config.Formatting.JoinConditionIndent = 4;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }


    [Fact]
    public void Select_LimitOffset()
    {

        var text = """
        SELECT b.title, b.published_year
        FROM books b
        WHERE b.published_year > 2000
        ORDER BY b.title
        LIMIT 10 OFFSET 5
        """;

        var formatted = """
        SELECT
            `b`.`title`,
            `b`.`published_year`
        FROM `books` AS `b`
        WHERE `b`.`published_year` > 2000
        ORDER BY `b`.`title`
        LIMIT 10 OFFSET 5;
        """;

        var (config, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables2);
        config.Formatting.ObjectNamePrefixWithSchema = false;
        config.Formatting.JoinConditionIndent = 4;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Select_IntoVarsEarly()
    {

        var text = """
        SELECT b.title, b.published_year
        INTO x, y
        FROM books b
        WHERE b.published_year > 2000
        ORDER BY b.title
        LIMIT 10 OFFSET 5
        """;

        var formatted = """
        SELECT
            `b`.`title`,
            `b`.`published_year`
        INTO `x`, `y`
        FROM `books` AS `b`
        WHERE `b`.`published_year` > 2000
        ORDER BY `b`.`title`
        LIMIT 10 OFFSET 5;
        """;

        var (config, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables2);
        config.Formatting.ObjectNamePrefixWithSchema = false;
        config.Formatting.JoinConditionIndent = 4;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Select_IntoTableEarly()
    {

        var text = """
        SELECT b.title, b.published_year
        INTO temp_table
        FROM books b
        WHERE b.published_year > 2000
        ORDER BY b.title
        LIMIT 10 OFFSET 5
        """;

        var formatted = """
        SELECT
            `b`.`title`,
            `b`.`published_year`
        INTO `temp_table`
        FROM `books` AS `b`
        WHERE `b`.`published_year` > 2000
        ORDER BY `b`.`title`
        LIMIT 10 OFFSET 5;
        """;

        var (config, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables2);
        config.Formatting.ObjectNamePrefixWithSchema = false;
        config.Formatting.JoinConditionIndent = 4;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Select_IntoOutfileLate()
    {
        var text = """
        SELECT b.title, b.published_year
        FROM books b
        WHERE b.published_year > 2000
        ORDER BY b.title
        LIMIT 10 OFFSET 5
        INTO OUTFILE 'myfile.txt'
        """;

        var formatted = """
        SELECT
            `b`.`title`,
            `b`.`published_year`
        FROM `books` AS `b`
        WHERE `b`.`published_year` > 2000
        ORDER BY `b`.`title`
        LIMIT 10 OFFSET 5
        INTO OUTFILE 'myfile.txt';
        """;

        var (config, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables2);
        config.Formatting.ObjectNamePrefixWithSchema = false;
        config.Formatting.JoinConditionIndent = 4;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Select_LineCommentBefore()
    {
        var text = "-- a comment\nSELECT 1";
        var formatted = """
        -- a comment
        SELECT
            1;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Select_BlockCommentBefore()
    {
        var text = "/* a comment */\nSELECT 1";
        var formatted = """
        /* a comment */
        SELECT
            1;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void MultiStatement_LineCommentBetween()
    {
        var text = "SELECT 1;\n-- between\nSELECT 2";
        var formatted = """
        SELECT
            1;
        -- between
        SELECT
            2;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    /// <summary>
    /// Exercises a combination of:
    /// - A line comment before the first statement (with leading tabs that are ignored)
    /// - A multi-line block comment before the second statement, indented with two
    ///   spaces that are stripped when the formatter re-emits at indent level 0
    /// - Newlines that produce a blank line between the block comment and the statement
    /// </summary>
    [Fact]
    public void MultiStatement_MixedNonSql_LineComment_BlockComment_Whitespace()
    {
        // The two leading tabs before SELECT 1 are ignored (whitespace before the keyword).
        // The two leading spaces before /* are stripped by CountSpacesBefore, so the
        // block comment lines get their indent reduced by 2: "    line2" → "  line2".
        // The two trailing newlines after */ produce one blank line before SELECT 2.
        var text = "-- comment 1\n\t\tSELECT 1;\n\n  /* line1\n    line2 */\n\nSELECT 2; -- line after";
        var formatted = """
        -- comment 1
        SELECT
            1;

        /* line1
          line2 */

        SELECT
            2;  -- line after
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    /// <summary>
    /// Exercises a combination of:
    /// - A multi-line block comment at the very start (no preceding whitespace, indent stays 0)
    /// - A line comment before the first statement
    /// - A multi-line block comment before the second statement, indented with one tab,
    ///   where the inner line uses a tab + spaces; the formatter reduces indent to 0
    /// - Multiple blank lines between statements
    /// </summary>
    [Fact]
    public void MultiStatement_MixedNonSql_BlockComments_LineComment_Tabs_Newlines()
    {
        // /* head\n   body */ → no preceding whitespace, originalSpaces=0, indent stays 0:
        //   "   body" keeps its 3 spaces unchanged.
        // \t/* c\n\t   d */ → originalSpaces = 1 tab = 4 spaces; at indent=0 delta=-4:
        //   "\t   d" has 4+3=7 spaces → 7-4=3 new spaces → "   d".
        // The two newlines in the PreNonSql of SELECT 2 plus the \n from Format's ";\n"
        // give two blank lines between the statements.
        var text = "/* head\n   body */\n\n-- line comment\nSELECT 1;\n\n\t/* c\n\t   d */\nSELECT 2";
        var formatted = """
        /* head
           body */

        -- line comment
        SELECT
            1;

        /* c
           d */
        SELECT
            2;
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void ValuesQuery_SingleRow_AboveThreshold()
    {
        var text = "VALUES (1, 'a', mycol)";
        var formatted = """
        VALUES
        (
            1,
            'a',
            `mycol`
        );
        """;

        var (config, formatter) = Helpers.CreateFormatter(null);
        config.Formatting.ValueListMultiLineThreshold = 3;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void ValuesQuery_SingleRow_BelowThreshold()
    {
        var text = "VALUES (1, 'a', mycol)";
        var formatted = """
        VALUES
        (1, 'a', `mycol`);
        """;

        var (config, formatter) = Helpers.CreateFormatter(null);
        config.Formatting.ValueListMultiLineThreshold = 4;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void ValuesQuery_SingleRow_RowConstructor_AboveThreshold_OpenParenNewLine()
    {
        var text = "VALUES ROW(1, 'a', `mycol1`)";
        var formatted = """
        VALUES
        ROW
        (
            1,
            'a',
            `mycol1`
        );
        """;

        var (config, formatter) = Helpers.CreateFormatter(null);
        config.Formatting.ValueListMultiLineThreshold = 3;
        config.Formatting.OpeningParensOnNewLine = true;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void ValuesQuery_SingleRow_RowConstructor_AboveThreshold_NoOpenParenNewLine()
    {
        var text = "VALUES ROW(1, 'a', `mycol1`)";
        var formatted = """
        VALUES
        ROW (
            1,
            'a',
            `mycol1`
        );
        """;

        var (config, formatter) = Helpers.CreateFormatter(null);
        config.Formatting.ValueListMultiLineThreshold = 3;
        config.Formatting.OpeningParensOnNewLine = false;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void ValuesQuery_SingleRow_RowConstructor_BelowThreshold()
    {
        var text = "VALUES ROW(1, 'a', `mycol1`)";
        var formatted = """
        VALUES
        ROW (1, 'a', `mycol1`);
        """;

        var (config, formatter) = Helpers.CreateFormatter(null);
        config.Formatting.ValueListMultiLineThreshold = 4;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void ValuesQuery_MultipleRows_AboveThreshold()
    {
        var text = "VALUES (1, 'a', `mycol`), (2, 'b', `mycol`)";
        var formatted = """
        VALUES
        (
            1,
            'a',
            `mycol`
        ),
        (
            2,
            'b',
            `mycol`
        );
        """;

        var (config, formatter) = Helpers.CreateFormatter(null);
        config.Formatting.ValueListMultiLineThreshold = 3;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void ValuesQuery_MultipleRows_BelowThreshold()
    {
        var text = "VALUES (1, 'a', `mycol`), (2, 'b', `mycol`)";
        var formatted = """
        VALUES
        (1, 'a', `mycol`),
        (2, 'b', `mycol`);
        """;

        var (config, formatter) = Helpers.CreateFormatter(null);
        config.Formatting.ValueListMultiLineThreshold = 4;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void ValuesQuery_MultipleRows_RowConstructor_AboveThreshold_OpenParenNewLine()
    {
        var text = "VALUES ROW(1, 'a', `mycol1`), ROW(2, 'b', `mycol2`)";
        var formatted = """
        VALUES
        ROW
        (
            1,
            'a',
            `mycol1`
        ),
        ROW
        (
            2,
            'b',
            `mycol2`
        );
        """;

        var (config, formatter) = Helpers.CreateFormatter(null);
        config.Formatting.ValueListMultiLineThreshold = 3;
        config.Formatting.OpeningParensOnNewLine = true;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void ValuesQuery_MultipleRows_RowConstructor_AboveThreshold_NoOpenParenNewLine()
    {
        var text = "VALUES ROW(1, 'a', `mycol1`), ROW(2, 'b', `mycol2`)";
        var formatted = """
        VALUES
        ROW (
            1,
            'a',
            `mycol1`
        ),
        ROW (
            2,
            'b',
            `mycol2`
        );
        """;

        var (config, formatter) = Helpers.CreateFormatter(null);
        config.Formatting.ValueListMultiLineThreshold = 3;
        config.Formatting.OpeningParensOnNewLine = false;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void ValuesQuery_MultipleRows_RowConstructor_BelowThreshold()
    {
        var text = "VALUES ROW(1, 'a', `mycol1`), ROW(2, 'b', `mycol2`)";
        var formatted = """
        VALUES
        ROW (1, 'a', `mycol1`),
        ROW (2, 'b', `mycol2`);
        """;

        var (config, formatter) = Helpers.CreateFormatter(null);
        config.Formatting.ValueListMultiLineThreshold = 4;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }
}
