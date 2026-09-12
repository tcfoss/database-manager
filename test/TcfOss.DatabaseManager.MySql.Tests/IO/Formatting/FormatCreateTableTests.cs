using TcfOss.DatabaseManager.Core.Configuration.Attributes;

namespace TcfOss.DatabaseManager.MySql.Tests.IO.Formatting;

public class FormatCreateTableTests
{
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
        var (_, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
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

        var (_, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
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

        var (config, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
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

        var (_, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1, preferTabs: true);
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

        var (config, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
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

        var (_, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
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

        var (_, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
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

        var (_, formatter) = Helpers.CreateFormatter(Helpers.s_pseudoTables1);
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

        var (_, formatter) = Helpers.CreateFormatter(null);
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

        var (_, formatter) = Helpers.CreateFormatter(null);
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

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }


    [Fact]
    public void CreateTable_CommentsBeforeAndAfter()
    {
        var text = "-- a comment\nCREATE TABLE t (id INT); /*end comment*/";
        var formatted = """
        -- a comment
        CREATE TABLE `t`
        (
            `id` INT
        ); /*end comment*/
        """;

        var (_, formatter) = Helpers.CreateFormatter(null);
        using (formatter)
        {
            var actual = formatter.GetFormatted(text);
            Assert.Equal(formatted, actual, ignoreLineEndingDifferences: true);
        }
    }
}
