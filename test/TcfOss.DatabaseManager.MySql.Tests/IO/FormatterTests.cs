using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.MySql.BuiltIn;
using TcfOss.DatabaseManager.MySql.Lexing;
using TcfOss.DatabaseManager.MySql.Parsing;

namespace TcfOss.DatabaseManager.MySql.Tests.IO;

public class FormatterTests
{
    private static readonly PseudoTableSet s_pseudoTables1;
    private static readonly PseudoTableSet s_pseudoTables2;
    private static readonly PseudoTableSet s_pseudoTables3;

    static FormatterTests()
    {
        var schema = new SchemaIdentifier("schema1", new CatalogIdentifier("def", QuoteStyle.Backticks), QuoteStyle.Backticks);
        s_pseudoTables1 = new PseudoTableSet(schema, [
            new PseudoTable("books", new ObjectIdentifier("books", schema), ["title", "author_id", "published_year"], PseudoTableType.Table),
            new PseudoTable("authors", new ObjectIdentifier("authors", schema), ["id", "name"], PseudoTableType.Table),
        ]);

        s_pseudoTables2 = new PseudoTableSet(schema, [
            new PseudoTable("books", new ObjectIdentifier("books", schema), ["book_id", "title", "published_year", "description", "year_added"], PseudoTableType.Table),
            new PseudoTable("contributors", new ObjectIdentifier("contributors", schema), ["contributor_id", "first_name", "last_name", "description"], PseudoTableType.Table),
            new PseudoTable("book_authors", new ObjectIdentifier("book_authors", schema), ["book_id", "contributor_id", "list_order"], PseudoTableType.Table),
        ]);

        s_pseudoTables3 = new PseudoTableSet(schema, [
            new PseudoTable("table1", new ObjectIdentifier("table1", schema), ["id", "name", "value"], PseudoTableType.Table),
            new PseudoTable("table2", new ObjectIdentifier("table2", schema), ["id", "name", "description"], PseudoTableType.Table),
        ]);
    }

    private static (ConfigBase, Formatter) CreateFormatter(
        PseudoTableSet? pseudoTables,
        bool preferTabs = false
    )
    {
        var config = TestConfig.GetMyTestConfig();

        config.Formatting.ObjectNamePrefixWithSchema = true;
        config.Formatting.OmitModifiersIfDefault = true;
        config.Formatting.OpeningParensOnNewLine = true;
        config.Formatting.PreferTabs = false;
        config.Formatting.TabSize = 4;

        config.Formatting.PreferTabs = preferTabs;
        var formatter = new Formatter(config, new TextParser(new MyLexer(), new MyParser()), new MyFunctionNameProvider())
        {
            PseudoTables = pseudoTables?.CloneExternalOnly(),
        };
        return (config, formatter);
    }

    [Fact]
    public void CreateTable()
    {
        var text = """
          CREATE TABLE my_table (
        id INT PRIMARY KEY,parent_id INT NOT NULL,
           name VARCHAR(100) NOT NULL,
            age INT NOT NULL,
              last_name VARCHAR(100) NOT NULL,
          location POINT NOT NULL,
         created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
           PRIMARY KEY (id),
           INDEX idx_parent_id (parent_id),
         unique key uk_fullname (name (5) desc, last_name),
                UNIQUE KEY unique_name (name),
             CONSTRAINT ck_age CHECK (age >= 0),
                    SPATIAL INDEX idx_location (location) COMMENT 'Location index',
            CONSTRAINT fk_parent FOREIGN KEY (parent_id) REFERENCES parent_table(id)
            ) DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        """;

        var formatted = """
        CREATE TABLE `my_table`
        (
            `id` INT PRIMARY KEY,
            `parent_id` INT NOT NULL,
            `name` VARCHAR(100) NOT NULL,
            `age` INT NOT NULL,
            `last_name` VARCHAR(100) NOT NULL,
            `location` POINT NOT NULL,
            `created_at` DATETIME DEFAULT (CURRENT_TIMESTAMP()),
            PRIMARY KEY (`id`),
            INDEX `idx_parent_id` (`parent_id`),
            UNIQUE KEY `uk_fullname` (`name`(5) DESC, `last_name`),
            UNIQUE KEY `unique_name` (`name`),
            CONSTRAINT `ck_age` CHECK (`age` >= 0),
            SPATIAL INDEX `idx_location` (`location`) COMMENT 'Location index',
            CONSTRAINT `fk_parent` FOREIGN KEY (`parent_id`) REFERENCES `parent_table` (`id`)
        ) DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;
        """;
        var (_, formatter) = CreateFormatter(s_pseudoTables1);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Theory]
    [InlineData(null, null, null, false)]
    [InlineData("CASCADE", null, null, false)]
    [InlineData(null, "SET NULL", null, false)]
    [InlineData(null, null, "'Foreign key comment'", false)]
    [InlineData(null, null, "'Foreign key comment'", true)]
    [InlineData("CASCADE", "SET NULL", null, false)]
    [InlineData("CASCADE", null, "'Foreign key comment'", false)]
    [InlineData(null, "SET NULL", "'Foreign key comment'", true)]
    public void CreateTable_ForeignKeyCombols(string? onDelete, string? onUpdate, string? comment, bool commentEquals)
    {
        var onDeleteSql = onDelete != null ? $" ON DELETE {onDelete}" : "";
        var onUpdateSql = onUpdate != null ? $" ON UPDATE {onUpdate}" : "";
        string? commentSql = null;
        if (comment != null)
        {
            commentSql = commentEquals ? $" COMMENT = {comment}" : $" COMMENT {comment}";
        }

        var text = $"""
          CREATE TABLE my_table (
        id INT PRIMARY KEY,
            parent_id INT,
            CONSTRAINT fk_parent FOREIGN KEY (parent_id) REFERENCES parent_table(id){onDeleteSql}{onUpdateSql}{commentSql}
            ) DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        """;

        var formatted = $"""
        CREATE TABLE `my_table`
        (
            `id` INT PRIMARY KEY,
            `parent_id` INT,
            CONSTRAINT `fk_parent` FOREIGN KEY (`parent_id`) REFERENCES `parent_table` (`id`){onDeleteSql}{onUpdateSql}{commentSql}
        ) DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;
        """;

        var (_, formatter) = CreateFormatter(s_pseudoTables1);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateTable_NoNewLine()
    {
        var text = """
          CREATE TABLE my_table (
        id INT PRIMARY KEY,parent_id INT NOT NULL,
           name VARCHAR(100) NOT NULL,
         created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                description TEXT,
           PRIMARY KEY USING BTREE (id),
           KEY idx_parent_id (parent_id),
                CONSTRAINT unique_name UNIQUE KEY USING BTREE (name),
                KEY idx_last_name ((SUBSTRING(name, POSITION(' ' IN name) + 1))),
                FULLTEXT INDEX idx_description (description) COMMENT = 'Fulltext index',
            CONSTRAINT fk_parent FOREIGN KEY (parent_id) REFERENCES parent_table(id)
            ) DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        """;

        var formatted = """
        CREATE TABLE `my_table` (
            `id` INT PRIMARY KEY,
            `parent_id` INT NOT NULL,
            `name` VARCHAR(100) NOT NULL,
            `created_at` DATETIME DEFAULT (CURRENT_TIMESTAMP()),
            `description` TEXT,
            PRIMARY KEY USING BTREE (`id`),
            KEY `idx_parent_id` (`parent_id`),
            CONSTRAINT `unique_name` UNIQUE KEY USING BTREE (`name`),
            KEY `idx_last_name` ((SUBSTRING(`name`, POSITION(' ' IN `name`) + 1))),
            FULLTEXT INDEX `idx_description` (`description`) COMMENT = 'Fulltext index',
            CONSTRAINT `fk_parent` FOREIGN KEY (`parent_id`) REFERENCES `parent_table` (`id`)
        ) DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;
        """;

        var (config, formatter) = CreateFormatter(s_pseudoTables1);
        using (formatter)
        {
            config.Formatting.OpeningParensOnNewLine = false;
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateTable_PreferTabs()
    {
        var text = """
          CREATE TABLE my_table (
        id INT PRIMARY KEY,parent_id INT NOT NULL,
           name VARCHAR(100) NOT NULL,
         created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
           PRIMARY KEY (id),
           INDEX idx_parent_id USING BTREE (parent_id) COMMENT 'Backing index for FK',
                UNIQUE KEY unique_name (name),
                     INDEX idx_id_squared ((id * id) DESC),
            CONSTRAINT fk_parent FOREIGN KEY (parent_id) REFERENCES parent_table(id)
            ) DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        """;

        var formatted = """
        CREATE TABLE `my_table`
        (
        	`id` INT PRIMARY KEY,
        	`parent_id` INT NOT NULL,
        	`name` VARCHAR(100) NOT NULL,
        	`created_at` DATETIME DEFAULT (CURRENT_TIMESTAMP()),
        	PRIMARY KEY (`id`),
        	INDEX `idx_parent_id` USING BTREE (`parent_id`) COMMENT 'Backing index for FK',
        	UNIQUE KEY `unique_name` (`name`),
        	INDEX `idx_id_squared` ((`id` * `id`) DESC),
        	CONSTRAINT `fk_parent` FOREIGN KEY (`parent_id`) REFERENCES `parent_table` (`id`)
        ) DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;
        """;

        var (_, formatter) = CreateFormatter(s_pseudoTables1, preferTabs: true);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void Create_Table_QuoteRemoveIfNotKeyword()
    {

        var text = """
          CREATE TABLE `my_table` (
        `id` INT PRIMARY KEY,`parent_id` INT NOT NULL,
           `name` VARCHAR(100) NOT NULL,
         `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
           PRIMARY KEY (id),
           INDEX idx_parent_id (parent_id),
                UNIQUE unique_name (name),
            CONSTRAINT fk_parent FOREIGN KEY (parent_id) REFERENCES parent_table(id)
            ) DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        """;

        var formatted = """
        CREATE TABLE my_table
        (
            id INT PRIMARY KEY,
            parent_id INT NOT NULL,
            name VARCHAR(100) NOT NULL,
            created_at DATETIME DEFAULT (CURRENT_TIMESTAMP()),
            PRIMARY KEY (id),
            INDEX idx_parent_id (parent_id),
            UNIQUE unique_name (name),
            CONSTRAINT fk_parent FOREIGN KEY (parent_id) REFERENCES parent_table (id)
        ) DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;
        """;

        var (config, formatter) = CreateFormatter(s_pseudoTables1);
        config.Formatting.Quoting = IdentifierQuotationHandling.OnlyIfSpecialOrFunction;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateTable_Temporary()
    {
        var text = """
            create temporary table `my_table` (
            id int not null primary key,
            name varchar(100) not null
            );
        """;

        var formatted = """
        CREATE TEMPORARY TABLE `my_table`
        (
            `id` INT NOT NULL PRIMARY KEY,
            `name` VARCHAR(100) NOT NULL
        );
        """;

        var (_, formatter) = CreateFormatter(s_pseudoTables1);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateTable_OrReplace()
    {
        var text = """
            create or replace table `my_table` (
               id int not null primary key,
         name varchar(100) not null
            );
        """;
        var formatted = """
        CREATE OR REPLACE TABLE `my_table`
        (
            `id` INT NOT NULL PRIMARY KEY,
            `name` VARCHAR(100) NOT NULL
        );
        """;

        var (_, formatter) = CreateFormatter(s_pseudoTables1);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateTable_IfNotExists()
    {
        var text = """
            create table if not exists `my_table` (id int not null primary key,
            name varchar(100) not null
            );
        """;

        var formatted = """
        CREATE TABLE IF NOT EXISTS `my_table`
        (
            `id` INT NOT NULL PRIMARY KEY,
            `name` VARCHAR(100) NOT NULL
        );
        """;

        var (_, formatter) = CreateFormatter(s_pseudoTables1);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

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

        var (_, formatter) = CreateFormatter(null);
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

        var (_, formatter) = CreateFormatter(null);
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

        var (config, formatter) = CreateFormatter(s_pseudoTables3);
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

        var (_, formatter) = CreateFormatter(null);
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

        var (_, formatter) = CreateFormatter(null);
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

        var (config, formatter) = CreateFormatter(s_pseudoTables1);
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

        var (config, formatter) = CreateFormatter(s_pseudoTables1);
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

        var (config, formatter) = CreateFormatter(s_pseudoTables1);
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

        var (config, formatter) = CreateFormatter(s_pseudoTables1);
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

        var (config, formatter) = CreateFormatter(s_pseudoTables2);
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

        var (config, formatter) = CreateFormatter(s_pseudoTables2);
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

        var (config, formatter) = CreateFormatter(s_pseudoTables2);
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

        var (config, formatter) = CreateFormatter(s_pseudoTables2);
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

        var (config, formatter) = CreateFormatter(s_pseudoTables2);
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

        var (config, formatter) = CreateFormatter(s_pseudoTables2);
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

        var (config, formatter) = CreateFormatter(s_pseudoTables2);
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

        var (config, formatter) = CreateFormatter(s_pseudoTables2);
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

        var (config, formatter) = CreateFormatter(s_pseudoTables2);
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

        var (config, formatter) = CreateFormatter(s_pseudoTables2);
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

        var (config, formatter) = CreateFormatter(s_pseudoTables2);
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

        var (config, formatter) = CreateFormatter(s_pseudoTables2);
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

        var (config, formatter) = CreateFormatter(s_pseudoTables2);
        config.Formatting.ObjectNamePrefixWithSchema = false;
        config.Formatting.JoinConditionIndent = 4;
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateTableAsSelect()
    {
        var text = """
        CREATE TABLE mytable AS SELECT my_id, my_val FROM othertable WHERE my_id > 10 ORDER BY my_val
        """;

        var formatted = """
        CREATE TABLE `mytable`
        AS
        SELECT
            `my_id`,
            `my_val`
        FROM `othertable`
        WHERE `my_id` > 10
        ORDER BY `my_val`;
        """;

        var (_, formatter) = CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateTableAsSelect_WithColumnsAndConstraints()
    {
        var text = """
        CREATE TABLE newbooks (title VARCHAR(255), publishyear year, UNIQUE (title)) AS SELECT title, published_year FROM books
        """;

        var formatted = """
        CREATE TABLE `newbooks`
        (
            `title` VARCHAR(255),
            `publishyear` YEAR,
            UNIQUE (`title`)
        )
        AS
        SELECT
            `title`,
            `published_year`
        FROM `books`;
        """;

        var (_, formatter) = CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Fact]
    public void CreateTableAsSelect_WithColumnsAndConstraintsAndOptions()
    {
        var text = """
        CREATE TABLE newbooks (title VARCHAR(255), publishyear year, UNIQUE (title)) ENGINE = InnoDB default charset utf8mb4  AS SELECT title, published_year FROM books
        """;

        var formatted = """
        CREATE TABLE `newbooks`
        (
            `title` VARCHAR(255),
            `publishyear` YEAR,
            UNIQUE (`title`)
        ) ENGINE = InnoDB DEFAULT CHARSET utf8mb4
        AS
        SELECT
            `title`,
            `published_year`
        FROM `books`;
        """;

        var (_, formatter) = CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }

    [Theory]
    [InlineData("CALL CONCAT('a', 'b')", "CALL CONCAT('a', 'b');", Label = "Call built-in function")]
    [InlineData("CALL `myfunc`('a', 'b')", "CALL `myfunc`('a', 'b');", Label = "Call quoted user-defined function")]
    [InlineData("CALL myfunc('a', 'b')", "CALL `myfunc`('a', 'b');", Label = "Call user-defined function")]
    [InlineData("CALL schema1.myfunc('a', 'b')", "CALL `schema1`.`myfunc`('a', 'b');", Label = "Call user-defined function with schema")]
    public void Format_Components(string input, string expected)
    {
        var (_, formatter) = CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(input);
            Assert.Equal(expected, actual);
        }
    }

}
