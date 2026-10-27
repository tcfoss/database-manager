using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.Core.Tests.ParserTests;

public class TableParserTests : ParserTestsBase<GenericLexer, Parser>
{
    [Theory]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL PRIMARY KEY, my_val VARCHAR(30) NOT NULL UNIQUE)", Label = "column primary and unique")]
    [InlineData("CREATE OR REPLACE TABLE IF NOT EXISTS mytable (my_id INT)", Label = "replace if not exists")]
    [InlineData("CREATE TABLE mytable (my_id INT, CONSTRAINT pk_mytable_my_id PRIMARY KEY (`my_id`))", Label = "named primary key")]
    [InlineData("CREATE TABLE mytable (my_id INT, CONSTRAINT ck_mytable_nonneg CHECK (my_id > 0))", Label = "named check")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL PRIMARY KEY, other_id INT NOT NULL, FOREIGN KEY (other_id) REFERENCES othertable (other_id))", Label = "foreign key")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL PRIMARY KEY, other_id INT NOT NULL, FOREIGN KEY (other_id) REFERENCES othertable (other_id) COMMENT 'Foreign key to othertable')", Label = "foreign key comment")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL PRIMARY KEY, other_id INT NOT NULL, FOREIGN KEY (other_id) REFERENCES othertable (other_id) ON DELETE CASCADE ON UPDATE RESTRICT)", Label = "foreign key cascade restrict")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL PRIMARY KEY, other_id INT NOT NULL, FOREIGN KEY (other_id) REFERENCES othertable (other_id) ON DELETE SET NULL ON UPDATE SET DEFAULT)", Label = "foreign key set null default")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL PRIMARY KEY, other_id INT NOT NULL, FOREIGN KEY (other_id) REFERENCES othertable (other_id) ON DELETE NO ACTION ON UPDATE NO ACTION)", Label = "foreign key no action")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL PRIMARY KEY, other_id INT NOT NULL, CONSTRAINT fk_mytable_othertable FOREIGN KEY (other_id) REFERENCES othertable (other_id))", Label = "named foreign key")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL IDENTITY PRIMARY KEY)", Label = "identity default")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL IDENTITY(5, 5) PRIMARY KEY)", Label = "identity explicit seed")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL PRIMARY KEY AUTO_INCREMENT)", Label = "auto increment primary key")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5) DEFAULT 'x')", Label = "string default")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5) DEFAULT ('x'))", Label = "parenthesized default")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5) CONSTRAINT df_mytable_mod_dt DEFAULT ('x' + 'y'))", Label = "Column default expression")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL COMMENT 'Hi')", Label = "column comment")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL COMMENT = 'Hi')", Label = "column comment equals")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), INDEX my_index (my_id, my_val))", Label = "index columns")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), INDEX my_index (my_id, my_val) COMMENT 'My index')", Label = "index comment")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), INDEX my_index (my_id, my_val(5)))", Label = "index prefix length")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), INDEX my_index (my_id, my_val(5) ASC))", Label = "index prefix asc")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), INDEX my_index (my_id, my_val(5) DESC))", Label = "index prefix desc")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), INDEX my_index (my_id, my_val ASC))", Label = "index asc")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), INDEX my_index ((my_id + 1)))", Label = "computed index")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), INDEX my_index ((my_id + 1) DESC))", Label = "computed index desc")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), INDEX my_index (SUBSTRING(my_val, 1, 3)))", Label = "computed index function")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), INDEX my_index (my_val) INCLUDE (my_id))", Label = "index include one")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val1 VARCHAR(5), my_val2 VARCHAR(5), INDEX my_index (my_val1) INCLUDE (my_val2, my_id))", Label = "index include multiple")]
    public static void CreateTable_TextMatch(string sql)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.Parse, sql);

        Assert.Equal(expected, actual);
    }

    /* Spatial and Fulltext */
    [Theory]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), SPATIAL (my_val))", Label = "spatial index")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), SPATIAL my_key (my_val))", Label = "spatial index with key")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), FULLTEXT (my_val))", Label = "fulltext index")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), FULLTEXT my_key (my_val))", Label = "fulltext index with key")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), FULLTEXT my_key (my_val) COMMENT = 'Fulltext index')", Label = "fulltext index with comment")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), SPATIAL INDEX my_key (my_val))", Label = "spatial index with key definition")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), FULLTEXT INDEX my_key (my_val))", Label = "fulltext index with key definition")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), SPATIAL KEY my_key (my_val))", Label = "spatial key index")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), FULLTEXT KEY my_key (my_val))", Label = "fulltext key index")]
    public static void CreateTable_SpatialAndFulltext_TextMatch(string sql)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.Parse, sql);

        Assert.Equal(expected, actual);
    }

    /* Checks */
    [Theory]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5) CHECK (my_val > 'a'))", Label = "column check")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5) CONSTRAINT ck_check CHECK (my_val > 'a'))", Label = "named column check")]
    public static void CreateTable_Checks_TextMatch(string sql)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.Parse, sql);

        Assert.Equal(expected, actual);
    }

    /* Generation */
    [Theory]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val BIGINT GENERATED ALWAYS AS (3 + 4))", Label = "generated always")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val BIGINT AS (3 + 4))", Label = "generated as")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val BIGINT GENERATED ALWAYS AS (3 + 4) PERSISTENT)", Label = "generated persistent")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val BIGINT GENERATED ALWAYS AS (3 + 4) STORED)", Label = "generated stored")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val BIGINT GENERATED ALWAYS AS (3 + 4) VIRTUAL)", Label = "generated virtual")]
    public static void CreateTable_Generation_TextMatch(string sql)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.Parse, sql);

        Assert.Equal(expected, actual);
    }

    /* Unique Indexes */
    [Theory]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), UNIQUE (my_val))", Label = "unique unnamed")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), UNIQUE KEY key_name (my_val))", Label = "unique key named")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), UNIQUE KEY (my_val))", Label = "unique key unnamed")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), UNIQUE INDEX (my_val))", Label = "unique index unnamed")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), UNIQUE KEY (my_id, my_val))", Label = "unique key multiple columns")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), UNIQUE KEY (my_id, my_val(5)))", Label = "unique key prefix")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), UNIQUE KEY (my_id, my_val DESC))", Label = "unique key desc")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), UNIQUE KEY (my_id, my_val(5) DESC))", Label = "unique key prefix desc")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), UNIQUE KEY (my_id) COMMENT 'Unique index')", Label = "unique key comment")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), UNIQUE INDEX my_index (my_val) INCLUDE (my_id))", Label = "unique index include one")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val1 VARCHAR(5), my_val2 VARCHAR(5), UNIQUE INDEX my_index (my_val1) INCLUDE (my_val2, my_id))", Label = "unique index include multiple")]
    public static void CreateTable_UniqueIndexes_TextMatch(string sql)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.Parse, sql);

        Assert.Equal(expected, actual);
    }

    /* Standard Indexes */
    [Theory]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, INDEX my_index (my_id))", Label = "named index")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, KEY my_index (my_id))", Label = "named key")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, INDEX (my_id))", Label = "unnamed index")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, KEY (my_id))", Label = "unnamed key")]
    public static void CreateTable_StandardIndexes_TextMatch(string sql)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.Parse, sql);

        Assert.Equal(expected, actual);
    }

    /* Index Methods */
    [Theory]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, PRIMARY KEY USING BTREE (my_id))", Label = "primary key btree")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, PRIMARY KEY USING HASH (my_id))", Label = "primary key hash")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, UNIQUE INDEX uidx USING BTREE (my_id))", Label = "unique index btree")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, UNIQUE KEY uidx USING BTREE (my_id))", Label = "unique key btree")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, UNIQUE INDEX uidx USING HASH (my_id))", Label = "unique index hash")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, UNIQUE KEY uidx USING HASH (my_id))", Label = "unique key hash")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, INDEX my_index USING BTREE (my_id))", Label = "index btree")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, INDEX my_index USING HASH (my_id))", Label = "index hash")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, INDEX my_index USING RTREE (my_id))", Label = "index rtree")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, INDEX my_index USING GIST (my_id))", Label = "index gist")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, KEY my_index USING SPGIST (my_id))", Label = "key spgist")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, KEY my_index USING GIN (my_id))", Label = "key gin")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, KEY my_index USING BRIN (my_id))", Label = "key brin")]
    public static void CreateTable_IndexMethods_TextMatch(string sql)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.Parse, sql);

        Assert.Equal(expected, actual);
    }

    /* Index Organization */
    [Theory]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, PRIMARY KEY CLUSTERED (my_id))", Label = "primary key clustered")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, PRIMARY KEY NONCLUSTERED (my_id))", Label = "primary key nonclustered")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), CONSTRAINT my_index UNIQUE CLUSTERED (my_val))", Label = "unique constraint clustered")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), CONSTRAINT my_index UNIQUE NONCLUSTERED (my_val))", Label = "unique constraint nonclustered")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), INDEX my_index UNIQUE CLUSTERED (my_val))", Label = "unique index clustered")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), INDEX my_index UNIQUE NONCLUSTERED (my_val))", Label = "unique index nonclustered")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), INDEX my_index CLUSTERED (my_val))", Label = "index clustered")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), INDEX my_index NONCLUSTERED (my_val))", Label = "index nonclustered")]
    public static void CreateTable_IndexOrganization_TextMatch(string sql)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.Parse, sql);

        Assert.Equal(expected, actual);
    }

    /* MS-SQL-style Index Filter/Options/Storage */
    [Theory]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), INDEX my_index (my_id) WHERE my_val IS NOT NULL WITH (PAD_INDEX = ON, FILLFACTOR = 80, IGNORE_DUP_KEY = OFF, STATISTICS_NORECOMPUTE = ON, STATISTICS_INCREMENTAL = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = ON) ON fg_my_index)", Label = "index filter options filegroup")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), INDEX my_unique_index UNIQUE (my_id) WHERE my_id > 0 WITH (PAD_INDEX = OFF, FILLFACTOR = 90) ON ps_my_index (my_id))", Label = "unique index filter partition scheme")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, PRIMARY KEY (my_id) WITH (PAD_INDEX = ON, IGNORE_DUP_KEY = OFF) ON fg_my_pk)", Label = "primary key options filegroup")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), CONSTRAINT uq_mytable_my_val UNIQUE (my_val) WITH (ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = OFF) ON ps_my_uq (my_val))", Label = "unique constraint options partition scheme")]
    public static void CreateTable_IndexOptions_TextMatch(string sql)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.Parse, sql);

        Assert.Equal(expected, actual);
    }

    /* As SELECT */
    [Theory]
    [InlineData("CREATE TABLE mytable AS SELECT my_id, my_val FROM othertable", Label = "as select")]
    [InlineData("CREATE TABLE mytable AS SELECT my_id, my_val FROM othertable WHERE my_id > 10", Label = "as select where")]
    [InlineData("CREATE TABLE mytable AS SELECT my_id, my_val FROM othertable WHERE my_id > 10 ORDER BY my_val", Label = "as select order by")]
    [InlineData("CREATE TABLE mytable (new_id INT, new_val VARCHAR(20)) AS SELECT my_id, my_val FROM othertable", Label = "as select with columns")]
    [InlineData("CREATE TABLE mytable (new_id INT, new_val VARCHAR(20), PRIMARY KEY (new_id), UNIQUE KEY (new_id)) AS SELECT my_id, my_val FROM othertable", Label = "as select with constraints")]
    public static void CreateTable_AsSelect_TextMatch(string sql)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.Parse, sql);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public static void CreateTable_Column_SymbolDataType_Throws()
    {
        var sql = "CREATE TABLE mytable (my_id INT NOT NULL, my_val +)";
        var parser = new Parser();
        var ex = Assert.Throws<ParseException.ExpectedButFound>(() => parser.Parse(GetTokens(sql)));
        Assert.Contains("+", ex.Message);
        Assert.Contains("data_type", ex.Message);
    }

    [Theory]
    [InlineData("PRIMARY KEY USING BTREE (my_id)", null, "PRIMARY KEY", "BTREE")]
    [InlineData("PRIMARY KEY (my_id) USING BTREE", null, "PRIMARY KEY", "BTREE")]
    [InlineData("UNIQUE KEY uidx USING BTREE (my_id)", "uidx", "UNIQUE KEY", "BTREE")]
    [InlineData("UNIQUE KEY uidx (my_id) USING BTREE", "uidx", "UNIQUE KEY", "BTREE")]
    [InlineData("UNIQUE INDEX uidx USING HASH (my_id)", "uidx", "UNIQUE INDEX", "HASH")]
    [InlineData("UNIQUE INDEX uidx (my_id) USING HASH", "uidx", "UNIQUE INDEX", "HASH")]
    [InlineData("INDEX my_index USING BTREE (my_id)", "my_index", "INDEX", "BTREE")]
    [InlineData("INDEX my_index (my_id) USING BTREE", "my_index", "INDEX", "BTREE")]
    public static void CreateTable_IndexMethod_MethodPrintedBeforeColumns(string sql, string? indexName, string constraintType, string indexMethod)
    {
        var createTable = $"CREATE TABLE mytable (my_id INT NOT NULL, {sql})";
        indexName = indexName != null ? $" {indexName}" : "";
        var expected = $"CREATE TABLE mytable (my_id INT NOT NULL, {constraintType}{indexName} USING {indexMethod} (my_id))";

        var parser = new Parser();
        var state = GetState(createTable);
        var actual = parser.ParseStatement(state).ToSql();
        Assert.Equal(expected, actual);
    }

    [Theory]
    /* Param specified more than once */
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL PRIMARY KEY NULL)")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL PRIMARY KEY PRIMARY KEY)")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL IDENTITY IDENTITY(1,1))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL AUTO_INCREMENT AUTO_INCREMENT)")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5) DEFAULT 'x' DEFAULT 'y')")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5) COMMENT 'Hi' COMMENT 'Hello')")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), INDEX my_index USING BTREE (my_val) USING BTREE)")]
    [InlineData("CREATE TABLE mytable (my_id INT, INDEX my_index USING BTREE USING HASH (my_id))")]
    [InlineData("CREATE TABLE mytable (my_id INT, INDEX my_index UNIQUE UNIQUE (my_id))")]
    [InlineData("CREATE TABLE mytable (my_id INT, INDEX first_name second_name (my_id))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), PRIMARY KEY USING BTREE (my_id) USING BTREE)")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), UNIQUE KEY my_index USING BTREE (my_id) USING BTREE)")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), UNIQUE KEY my_index USING BTREE (my_id) (my_val))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), UNIQUE KEY my_index USING BTREE (my_id) COMMENT 'Index' COMMENT 'Idx')")]
    public static void CreateTable_ValidationFailure_Duplicates(string sql)
    {
        var parser = new Parser();

        Assert.Throws<ParseException.Duplicate>(() => parser.Parse(GetTokens(sql)));
    }

    [Theory]
    [InlineData("PAD_INDEX = ON", "PAD_INDEX = OFF")]
    [InlineData("FILLFACTOR = 80", "FILLFACTOR = 90")]
    [InlineData("IGNORE_DUP_KEY = ON", "IGNORE_DUP_KEY = OFF")]
    [InlineData("STATISTICS_NORECOMPUTE = ON", "STATISTICS_NORECOMPUTE = OFF")]
    [InlineData("STATISTICS_INCREMENTAL = ON", "STATISTICS_INCREMENTAL = OFF")]
    [InlineData("ALLOW_ROW_LOCKS = ON", "ALLOW_ROW_LOCKS = OFF")]
    [InlineData("ALLOW_PAGE_LOCKS = ON", "ALLOW_PAGE_LOCKS = OFF")]
    [InlineData("OPTIMIZE_FOR_SEQUENTIAL_KEY = ON", "optimize_for_sequential_key = OFF")]
    public static void CreateTable_DuplicateIndexOption_Throws(string firstOption, string secondOption)
    {
        string sql = $"CREATE TABLE mytable (my_id INT, INDEX my_index (my_id) WITH ({firstOption}, {secondOption}))";

        Assert.Throws<ParseException.Duplicate>(() => ParseStatement(sql));
    }

    [Theory]
    /* Not allowed to be named */
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL CONSTRAINT gen_no_names_here GENERATED ALWAYS AS (a + b))", TestDisplayName = "Generated Column")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL CONSTRAINT gen_no_names_here AS (a + b))", TestDisplayName = "Generated Column Just AS")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL CONSTRAINT id_no_names_here IDENTITY(1,1))", TestDisplayName = "Identity")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL PRIMARY KEY CONSTRAINT ai_no_names_here AUTO_INCREMENT)", TestDisplayName = "Auto Increment")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL CONSTRAINT com_no_names_here COMMENT 'Hi'", TestDisplayName = "Column Comment")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL CONSTRAINT def_no_names_here ON UPDATE CURRENT_TIMESTAMP)", TestDisplayName = "On Update")]
    public static void CreateTable_ValidationFailure_Named(string sql)
    {
        var parser = new Parser();

        Assert.Throws<ParseException.ColumnComponentInvalidName>(() => parser.Parse(GetTokens(sql)));
    }

    [Theory]
    /* Wrong number of parameters */
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL IDENTITY(1))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL IDENTITY(1, 2, 3))")]
    /* Constraint name but no constraint */
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL CONSTRAINT ai_no_names_here")]
    public static void CreateTable_ValidationFailure_ExpectedButFound(string sql)
    {
        var parser = new Parser();

        Assert.Throws<ParseException.ExpectedButFound>(() => parser.Parse(GetTokens(sql)));
    }

    [Theory]
    /* Unknown index type */
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, INDEX my_index USING WHAT (my_id))")]
    /* Column or constraint expected */
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL +)")]
    [InlineData("CREATE TABLE mytable (my_id INT, INDEX my_index (my_id) WITH (unknown_option = ON))")]
    [InlineData("CREATE TABLE mytable (my_id INT, INDEX my_index (my_id) WITH (PAD_INDEX = invalid_value))")]
    [InlineData("CREATE TABLE mytable (my_id INT, INDEX my_index (my_id) WITH ())")]
    public static void CreateTable_ValidationFailure_ExpectedOneOfButFound(string sql)
    {
        var parser = new Parser();

        Assert.Throws<ParseException.ExpectedOneOfButFound>(() => parser.Parse(GetTokens(sql)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("FuLLTEXT")]
    [InlineData("SPATIAL")]
    public static void CreateTable_ConstraintBeforeNonConstraintKey_Throws(string? indexType)
    {
        var parser = new Parser();

        var ex = Assert.Throws<ParseException.ExpectedButFound>(() => parser.Parse(GetTokens($"CREATE TABLE mytable (my_id INT NOT NULL, CONSTRAINT my_constraint {indexType} INDEX my_index (my_id))")));
        Assert.Contains("table_constraint", ex.Message);
    }

    [Theory]
    [InlineData(QuoteStyle.Backticks)]
    [InlineData(QuoteStyle.Ansi)]
    [InlineData(QuoteStyle.Brackets)]
    public static void CreateTable_QuotedConstraint_TreatedAsColumn(QuoteStyle quoteStyle)
    {
#pragma warning disable CS8509 // The switch expression does not handle all possible values of its input type (it is not exhaustive).
        var quotedName = quoteStyle switch
        {
            QuoteStyle.Backticks => "`CONSTRAINT`",
            QuoteStyle.Ansi => "\"CONSTRAINT\"",
            QuoteStyle.Brackets => "[CONSTRAINT]",
        };
#pragma warning restore CS8509

        var parser = new Parser();
        var result = parser.Parse(GetTokens($"create table mytable ({quotedName} INT)"));
        var createTable = Assert.IsType<CreateTable>(Assert.Single(result));
        Assert.NotNull(createTable.Columns);
        var column = Assert.Single(createTable.Columns);
        Assert.Equal("CONSTRAINT", column.Name.Name);
    }

    [Fact]
    public static void CreateTable_Identity_InvalidValues_Throws()
    {
        var parser = new Parser();
        var sql = "CREATE TABLE mytable (my_id INT IDENTITY(CAT))";
        var ex = Assert.Throws<ParseException.ExpectedOneOfButFound>(() => parser.Parse(GetTokens(sql)));
        Assert.Contains(" ',' | ')' ", ex.Message);
    }

    [Fact]
    public static void CreateTable_NonsenseBody_Throws()
    {
        var parser = new Parser();
        var sql = "CREATE TABLE mytable (+)";
        var ex = Assert.Throws<ParseException.ExpectedButFound>(() => parser.Parse(GetTokens(sql)));
        Assert.Contains("column_name", ex.Message);
    }

    [Theory]
    /* Add Column */
    [InlineData("ALTER TABLE mytable ADD COLUMN new_col INT", null, Label = "add column")]
    [InlineData("ALTER TABLE mytable ADD COLUMN new_col INT NOT NULL", null, Label = "add not null column")]
    [InlineData("ALTER TABLE mytable ADD COLUMN new_col INT NOT NULL AFTER other_col", null, Label = "add column after")]
    [InlineData("ALTER TABLE mytable ADD COLUMN new_col INT NOT NULL FIRST", null, Label = "add column first")]
    [InlineData("ALTER TABLE mytable ADD new_col INT", null, Label = "add shorthand column")]
    [InlineData("ALTER TABLE mytable ADD new_col INT NOT NULL", null, Label = "add shorthand not null column")]
    [InlineData("ALTER TABLE mytable ADD new_col INT NOT NULL AFTER other_col", null, Label = "add shorthand column after")]
    public static void AlterTable_AddColumn_TextMatch(string sql, string? realExpected)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.Parse, sql);

        if (realExpected != null)
        {
            expected = realExpected;
        }
        Assert.Equal(expected, actual);
    }

    /* Column Defaults */
    [Theory]
    [InlineData("ALTER TABLE mytable ALTER COLUMN my_val SET DEFAULT 10", null, Label = "set numeric default")]
    [InlineData("ALTER TABLE mytable ALTER COLUMN my_val SET DEFAULT 'abc'", null, Label = "set string default")]
    [InlineData("ALTER TABLE mytable ALTER COLUMN my_val SET DEFAULT (a + b)", null, Label = "set expression default")]
    [InlineData("ALTER TABLE mytable ALTER COLUMN my_val DROP DEFAULT", null, Label = "drop default")]
    public static void AlterTable_ColumnDefaults_TextMatch(string sql, string? realExpected)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.Parse, sql);

        if (realExpected != null)
        {
            expected = realExpected;
        }
        Assert.Equal(expected, actual);
    }

    /* Change/Modify Columns */
    [Theory]
    [InlineData("ALTER TABLE mytable MODIFY COLUMN my_val VARCHAR(50) NOT NULL", null, Label = "modify column")]
    [InlineData("ALTER TABLE mytable CHANGE COLUMN my_val my_new_val VARCHAR(100) NULL", null, Label = "change column")]
    [InlineData("ALTER TABLE mytable CHANGE COLUMN my_val my_val VARCHAR(100) NULL", null, Label = "change column same name")]
    [InlineData("ALTER TABLE mytable MODIFY my_val VARCHAR(50) NOT NULL", "ALTER TABLE mytable MODIFY COLUMN my_val VARCHAR(50) NOT NULL", Label = "modify shorthand")]
    [InlineData("ALTER TABLE mytable CHANGE my_val my_new_val VARCHAR(100) NULL", "ALTER TABLE mytable CHANGE COLUMN my_val my_new_val VARCHAR(100) NULL", Label = "change shorthand")]
    [InlineData("ALTER TABLE mytable MODIFY COLUMN my_val INT NOT NULL AFTER other_col", null, Label = "modify column after")]
    [InlineData("ALTER TABLE mytable MODIFY COLUMN my_val INT NOT NULL FIRST", null, Label = "modify column first")]
    [InlineData("ALTER TABLE mytable CHANGE COLUMN my_val my_new_val INT NOT NULL AFTER other_col", null, Label = "change column after")]
    [InlineData("ALTER TABLE mytable CHANGE COLUMN my_val my_new_val INT NOT NULL FIRST", null, Label = "change column first")]
    public static void AlterTable_ChangeModifyColumns_TextMatch(string sql, string? realExpected)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.Parse, sql);

        if (realExpected != null)
        {
            expected = realExpected;
        }
        Assert.Equal(expected, actual);
    }

    /* Add Keys */
    [Theory]
    [InlineData("ALTER TABLE mytable ADD PRIMARY KEY (my_id)", null, Label = "add primary key")]
    [InlineData("ALTER TABLE mytable ADD CONSTRAINT uidx_mytable_my_val UNIQUE (my_val)", null, Label = "add named unique")]
    [InlineData("ALTER TABLE mytable ADD CONSTRAINT uidx_mytable_my_val UNIQUE KEY (my_val)", null, Label = "add named unique key")]
    [InlineData("ALTER TABLE mytable ADD CONSTRAINT uidx_mytable_my_val UNIQUE INDEX (my_val)", null, Label = "add named unique index")]
    [InlineData("ALTER TABLE mytable ADD UNIQUE (my_val)", null, Label = "add unnamed unique")]
    [InlineData("ALTER TABLE mytable ADD UNIQUE KEY (my_val)", null, Label = "add unnamed unique key")]
    [InlineData("ALTER TABLE mytable ADD UNIQUE INDEX (my_val)", null, Label = "add unnamed unique index")]
    [InlineData("ALTER TABLE mytable ADD UNIQUE KEY (my_val1, my_val2)", null, Label = "add multi-column unique key")]
    [InlineData("ALTER TABLE mytable ADD UNIQUE KEY uidx_mytable_my_val (my_val)", null, Label = "add named unique key index")]
    [InlineData("ALTER TABLE mytable ADD KEY idx_mytable_myval (my_val)", null, Label = "add key")]
    [InlineData("ALTER TABLE mytable ADD INDEX idx_mytable_myval (my_val)", null, Label = "add index")]
    [InlineData("ALTER TABLE mytable ADD INDEX idx_mytable_myval USING BTREE (my_val)", null, Label = "add btree index")]
    [InlineData("ALTER TABLE mytable ADD FULLTEXT INDEX idx_mytable_myval (my_val)", null, Label = "add fulltext index")]
    [InlineData("ALTER TABLE mytable ADD SPATIAL INDEX idx_mytable_myval (my_val)", null, Label = "add spatial index")]
    public static void AlterTable_AddKeys_TextMatch(string sql, string? realExpected)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.Parse, sql);

        if (realExpected != null)
        {
            expected = realExpected;
        }
        Assert.Equal(expected, actual);
    }

    /* Add Foreign Key */
    [Theory]
    [InlineData("ALTER TABLE mytable ADD FOREIGN KEY (other_id) REFERENCES othertable (other_id)", null, Label = "add unnamed foreign key")]
    [InlineData("ALTER TABLE mytable ADD CONSTRAINT fk_mytable_othertable FOREIGN KEY (other_id) REFERENCES othertable (other_id)", null, Label = "add named foreign key")]
    public static void AlterTable_AddForeignKey_TextMatch(string sql, string? realExpected)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.Parse, sql);

        if (realExpected != null)
        {
            expected = realExpected;
        }
        Assert.Equal(expected, actual);
    }

    /* Add Check */
    [Theory]
    [InlineData("ALTER TABLE mytable ADD CHECK (my_val > 0)", null, Label = "add unnamed check")]
    [InlineData("ALTER TABLE mytable ADD CONSTRAINT ck_mytable_nonneg CHECK (my_val > 0)", null, Label = "add named check")]
    public static void AlterTable_AddCheck_TextMatch(string sql, string? realExpected)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.Parse, sql);

        if (realExpected != null)
        {
            expected = realExpected;
        }
        Assert.Equal(expected, actual);
    }

    /* Drops */
    [Theory]
    [InlineData("ALTER TABLE mytable DROP COLUMN old_col", null, Label = "drop column")]
    [InlineData("ALTER TABLE mytable DROP old_col", "ALTER TABLE mytable DROP COLUMN old_col", Label = "drop shorthand column")]
    [InlineData("ALTER TABLE mytable DROP PRIMARY KEY pk_name", null, Label = "drop named primary key")]
    [InlineData("ALTER TABLE mytable DROP FOREIGN KEY fk_mytable_othertable", null, Label = "drop foreign key")]
    [InlineData("ALTER TABLE mytable DROP INDEX my_index", "ALTER TABLE mytable DROP KEY my_index", Label = "drop index")]
    [InlineData("ALTER TABLE mytable DROP KEY my_key", null, Label = "drop key")]
    [InlineData("ALTER TABLE mytable DROP PRIMARY KEY", null, Label = "drop primary key")]
    [InlineData("ALTER TABLE mytable DROP CHECK ck_mytable_nonneg", "ALTER TABLE mytable DROP CONSTRAINT ck_mytable_nonneg", Label = "drop check")]
    [InlineData("ALTER TABLE mytable DROP CONSTRAINT ck_mytable_nonneg", null, Label = "drop constraint")]
    public static void AlterTable_Drops_TextMatch(string sql, string? realExpected)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.Parse, sql);

        if (realExpected != null)
        {
            expected = realExpected;
        }
        Assert.Equal(expected, actual);
    }

    /* Table Renames */
    [Theory]
    [InlineData("ALTER TABLE mytable RENAME TO new_table_name", null, Label = "rename table to")]
    [InlineData("ALTER TABLE mytable RENAME AS new_table_name", "ALTER TABLE mytable RENAME TO new_table_name", Label = "rename table as")]
    [InlineData("ALTER TABLE mytable RENAME new_table_name", "ALTER TABLE mytable RENAME TO new_table_name", Label = "rename table shorthand")]
    public static void AlterTable_TableRenames_TextMatch(string sql, string? realExpected)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.Parse, sql);

        if (realExpected != null)
        {
            expected = realExpected;
        }
        Assert.Equal(expected, actual);
    }

    /* Column Renames */
    [Theory]
    [InlineData("ALTER TABLE mytable RENAME COLUMN old_col TO new_col", null, Label = "rename column")]
    public static void AlterTable_ColumnRenames_TextMatch(string sql, string? realExpected)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.Parse, sql);

        if (realExpected != null)
        {
            expected = realExpected;
        }
        Assert.Equal(expected, actual);
    }

    /* Table Options */
    [Theory]
    [InlineData("ALTER TABLE mytable AUTO_INCREMENT = 100", null, Label = "set auto increment")]
    public static void AlterTable_TableOptions_TextMatch(string sql, string? realExpected)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.Parse, sql);

        if (realExpected != null)
        {
            expected = realExpected;
        }
        Assert.Equal(expected, actual);
    }

    /* Default Character Set and Collation */
    [Theory]
    [InlineData("ALTER TABLE mytable DEFAULT CHARACTER SET utf8", null, Label = "default character set")]
    [InlineData("ALTER TABLE mytable DEFAULT COLLATE utf8_general_ci", null, Label = "default collation")]
    [InlineData("ALTER TABLE mytable DEFAULT CHARACTER SET utf8, DEFAULT COLLATE utf8_general_ci", null, Label = "default charset and collation")]
    [InlineData("ALTER TABLE mytable CHARACTER SET utf8, COLLATE utf8_general_ci", null, Label = "charset and collation")]
    [InlineData("ALTER TABLE mytable CHARACTER SET = utf8, COLLATE = utf8_general_ci", null, Label = "charset and collation with equals")]
    [InlineData("ALTER TABLE mytable CHARACTER SET utf8, DEFAULT COLLATE = utf8_general_ci", null, Label = "charset with default collation")]
    public static void AlterTable_CharacterSetAndCollation_TextMatch(string sql, string? realExpected)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.Parse, sql);

        if (realExpected != null)
        {
            expected = realExpected;
        }
        Assert.Equal(expected, actual);
    }

    /* Multiple Operations */
    [Theory]
    [InlineData("ALTER TABLE mytable ADD COLUMN new_col INT, ADD PRIMARY KEY (my_id)", null, Label = "multiple operations add")]
    [InlineData("ALTER TABLE mytable ADD COLUMN new_col INT, ADD PRIMARY KEY (my_id), DROP CONSTRAINT ck_mytable_nonneg", null, Label = "multiple operations add and drop")]
    public static void AlterTable_MultipleOperations_TextMatch(string sql, string? realExpected)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.Parse, sql);

        if (realExpected != null)
        {
            expected = realExpected;
        }
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("ALTER TABLE mytable DOUNKNOWNTHING blah")]
    public static void AlterTable_UnknownOperation_Throws(string sql)
    {
        var parser = new Parser();

        var exception = Assert.Throws<ParseException.ExpectedButFound>(() => parser.Parse(GetTokens(sql)));
        var expectedTemplate = "Expected {0}. Found {1}.";
        var expected = string.Format(expectedTemplate, "alter_table_operation".Italic(), "DOUNKNOWNTHING");
        Assert.Equal(expected, exception.Message);
    }

    [Fact]
    public void AlterTable_MissingOperation_Throws()
    {
        var parser = new Parser();
        var sql = "ALTER TABLE mytable";

        var ex = Assert.Throws<ParseException.ExpectedButFound>(() => parser.Parse(GetTokens(sql)));
        Assert.Contains("alter_table_operation", ex.Message);
    }

    [Fact]
    public void AlterTable_AlterColumn_Unknown_Throws()
    {
        var parser = new Parser();
        var sql = "ALTER TABLE mytable ALTER COLUMN my_col UNKNOWN";

        var ex = Assert.Throws<ParseException.ExpectedOneOfButFound>(() => parser.Parse(GetTokens(sql)));
        Assert.Contains("SET DEFAULT", ex.Message);
        Assert.Contains("DROP DEFAULT", ex.Message);
    }

    [Fact]
    public void AlterTable_MissingComma_Throws()
    {
        var parser = new Parser();
        var sql = "ALTER TABLE mytable ADD COLUMN new_col INT ADD PRIMARY KEY (my_id)";

        var ex = Assert.Throws<ParseException.ExpectedButFound>(() => parser.Parse(GetTokens(sql)));
        Assert.Contains(",", ex.Message);
    }

    [Fact]
    public void ReferentialAction_Unknown_Throws()
    {
        var state = new ParserState(GetTokens("DOUNKNOWNTHING"));

        var exception = Assert.Throws<ParseException.ExpectedOneOfButFound>(() => TableParser.ParseReferentialAction(state));
        var expectedTemplate = "Expected one of {{ {0} }}. Found {1}.";
        var expected = string.Format(expectedTemplate, "RESTRICT | CASCADE | SET NULL | NO ACTION | SET DEFAULT", "DOUNKNOWNTHING");
        Assert.Equal(expected, exception.Message);
    }
}
