using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Parsing;

namespace TcfOss.DatabaseManager.Core.Tests.ParserTests;

public class TableParserTests : ParserTestsBase<GenericLexer, Parser>
{
    [Theory]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL PRIMARY KEY, my_val VARCHAR(30) NOT NULL UNIQUE)")]
    [InlineData("CREATE OR REPLACE TABLE IF NOT EXISTS mytable (my_id INT)")]
    [InlineData("CREATE TABLE mytable (my_id INT, CONSTRAINT pk_mytable_my_id PRIMARY KEY (`my_id`))")]
    [InlineData("CREATE TABLE mytable (my_id INT, CONSTRAINT ck_mytable_nonneg CHECK (my_id > 0))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL PRIMARY KEY, other_id INT NOT NULL, FOREIGN KEY (other_id) REFERENCES othertable (other_id))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL PRIMARY KEY, other_id INT NOT NULL, FOREIGN KEY (other_id) REFERENCES othertable (other_id) COMMENT 'Foreign key to othertable')", Label = "FK with comment")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL PRIMARY KEY, other_id INT NOT NULL, FOREIGN KEY (other_id) REFERENCES othertable (other_id) ON DELETE CASCADE ON UPDATE RESTRICT)", Label = "FK ON DELETE CASCADE ON UPDATE RESTRICT")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL PRIMARY KEY, other_id INT NOT NULL, FOREIGN KEY (other_id) REFERENCES othertable (other_id) ON DELETE SET NULL ON UPDATE SET DEFAULT)", Label = "FK ON DELETE SET NULL ON UPDATE SET DEFAULT")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL PRIMARY KEY, other_id INT NOT NULL, FOREIGN KEY (other_id) REFERENCES othertable (other_id) ON DELETE NO ACTION ON UPDATE NO ACTION)", Label = "FK NO ACTION")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL PRIMARY KEY, other_id INT NOT NULL, CONSTRAINT fk_mytable_othertable FOREIGN KEY (other_id) REFERENCES othertable (other_id))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL IDENTITY PRIMARY KEY)")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL IDENTITY(5, 5) PRIMARY KEY)")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL PRIMARY KEY AUTO_INCREMENT)")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5) DEFAULT 'x')")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5) DEFAULT ('x'))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5) CONSTRAINT df_mytable_mod_dt DEFAULT ('x' + 'y'))", Label = "Column default expression")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL COMMENT 'Hi')")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL COMMENT = 'Hi')")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), INDEX my_index (my_id, my_val))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), INDEX my_index (my_id, my_val) COMMENT 'My index')")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), INDEX my_index (my_id, my_val(5)))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), INDEX my_index (my_id, my_val(5) ASC))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), INDEX my_index (my_id, my_val(5) DESC))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), INDEX my_index (my_id, my_val ASC))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), INDEX my_index ((my_id + 1)))", Label = "Computed Index Column")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), INDEX my_index ((my_id + 1) DESC))", Label = "Computed Index Column DESC")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), INDEX my_index (SUBSTRING(my_val, 1, 3)))", Label = "Computed Index Column with Function")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), INDEX my_index (my_val) INCLUDE (my_id))", Label = "Included Column")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val1 VARCHAR(5), my_val2 VARCHAR(5), INDEX my_index (my_val1) INCLUDE (my_val2, my_id))", Label = "Included Column Multiple")]
    /* Spatial and Fulltext */
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), SPATIAL my_key (my_val))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), FULLTEXT my_key (my_val))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), FULLTEXT my_key (my_val) COMMENT = 'Fulltext index')")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), SPATIAL INDEX my_key (my_val))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), FULLTEXT INDEX my_key (my_val))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), SPATIAL KEY my_key (my_val))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), FULLTEXT KEY my_key (my_val))")]
    /* Checks */
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5) CHECK (my_val > 'a'))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5) CONSTRAINT ck_check CHECK (my_val > 'a'))")]
    /* Generation */
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val BIGINT GENERATED ALWAYS AS (3 + 4))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val BIGINT AS (3 + 4))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val BIGINT GENERATED ALWAYS AS (3 + 4) PERSISTENT)")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val BIGINT GENERATED ALWAYS AS (3 + 4) STORED)")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val BIGINT GENERATED ALWAYS AS (3 + 4) VIRTUAL)")]
    /* Unique Indexes */
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), UNIQUE (my_val))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), UNIQUE KEY key_name (my_val))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), UNIQUE KEY (my_val))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), UNIQUE INDEX (my_val))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), UNIQUE KEY (my_id, my_val))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), UNIQUE KEY (my_id, my_val(5)))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), UNIQUE KEY (my_id, my_val DESC))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), UNIQUE KEY (my_id, my_val(5) DESC))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), UNIQUE KEY (my_id) COMMENT 'Unique index')")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), UNIQUE INDEX my_index (my_val) INCLUDE (my_id))", Label = "Unique Included Column")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val1 VARCHAR(5), my_val2 VARCHAR(5), UNIQUE INDEX my_index (my_val1) INCLUDE (my_val2, my_id))", Label = "Unique Included Column Multiple")]
    /* Index Methods */
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, PRIMARY KEY USING BTREE (my_id))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, PRIMARY KEY USING HASH (my_id))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, UNIQUE INDEX uidx USING BTREE (my_id))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, UNIQUE KEY uidx USING BTREE (my_id))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, UNIQUE INDEX uidx USING HASH (my_id))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, UNIQUE KEY uidx USING HASH (my_id))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, INDEX my_index USING BTREE (my_id))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, INDEX my_index USING HASH (my_id))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, INDEX my_index USING RTREE (my_id))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, INDEX my_index USING GIST (my_id))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, KEY my_index USING SPGIST (my_id))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, KEY my_index USING GIN (my_id))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, KEY my_index USING BRIN (my_id))")]
    /* Index Organization */
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, PRIMARY KEY CLUSTERED (my_id))", Label = "PRIMARY KEY CLUSTERED")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, PRIMARY KEY NONCLUSTERED (my_id))", Label = "PRIMARY KEY NONCLUSTERED")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), CONSTRAINT my_index UNIQUE CLUSTERED (my_val))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), CONSTRAINT my_index UNIQUE NONCLUSTERED (my_val))")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), INDEX my_index UNIQUE CLUSTERED (my_val))", Label = "INDEX UNIQUE CLUSTERED")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), INDEX my_index UNIQUE NONCLUSTERED (my_val))", Label = "INDEX UNIQUE NONCLUSTERED")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), INDEX my_index CLUSTERED (my_val))", Label = "INDEX CLUSTERED")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), INDEX my_index NONCLUSTERED (my_val))", Label = "INDEX NONCLUSTERED")]
    /* MS-SQL-style Index Filter/Options/Storage */
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), INDEX my_index (my_id) WHERE my_val IS NOT NULL WITH (PAD_INDEX = ON, FILLFACTOR = 80, IGNORE_DUP_KEY = OFF, STATISTICS_NORECOMPUTE = ON, STATISTICS_INCREMENTAL = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = ON) ON fg_my_index)", Label = "INDEX WHERE WITH all options ON filegroup")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), INDEX my_unique_index UNIQUE (my_id) WHERE my_id > 0 WITH (PAD_INDEX = OFF, FILLFACTOR = 90) ON ps_my_index (my_id))", Label = "UNIQUE INDEX WHERE WITH ON partition scheme")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, PRIMARY KEY (my_id) WITH (PAD_INDEX = ON, IGNORE_DUP_KEY = OFF) ON fg_my_pk)", Label = "PRIMARY KEY WITH ON")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL, my_val VARCHAR(5), CONSTRAINT uq_mytable_my_val UNIQUE (my_val) WITH (ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = OFF) ON ps_my_uq (my_val))", Label = "UNIQUE CONSTRAINT WITH ON")]
    /* As SELECT */
    [InlineData("CREATE TABLE mytable AS SELECT my_id, my_val FROM othertable")]
    [InlineData("CREATE TABLE mytable AS SELECT my_id, my_val FROM othertable WHERE my_id > 10")]
    [InlineData("CREATE TABLE mytable AS SELECT my_id, my_val FROM othertable WHERE my_id > 10 ORDER BY my_val", Label = "CREATE AS SELECT with ORDER BY")]
    [InlineData("CREATE TABLE mytable (new_id INT, new_val VARCHAR(20)) AS SELECT my_id, my_val FROM othertable")]
    [InlineData("CREATE TABLE mytable (new_id INT, new_val VARCHAR(20), PRIMARY KEY (new_id), UNIQUE KEY (new_id)) AS SELECT my_id, my_val FROM othertable")]
    public static void CreateTable_TextMatch(string sql)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.Parse, sql);

        Assert.Equal(expected, actual);
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
    public static void CreateTable_ValidationFailure_ExpectedOneOfButFound(string sql)
    {
        var parser = new Parser();

        Assert.Throws<ParseException.ExpectedOneOfButFound>(() => parser.Parse(GetTokens(sql)));
    }

    [Theory]
    /* Add Column */
    [InlineData("ALTER TABLE mytable ADD COLUMN new_col INT", null)]
    [InlineData("ALTER TABLE mytable ADD COLUMN new_col INT NOT NULL", null)]
    [InlineData("ALTER TABLE mytable ADD new_col INT", "ALTER TABLE mytable ADD COLUMN new_col INT")]
    [InlineData("ALTER TABLE mytable ADD COLUMN new_col INT NOT NULL AFTER other_col", null)]
    [InlineData("ALTER TABLE mytable ADD COLUMN new_col INT NOT NULL FIRST", null)]
    /* Column Defaults */
    [InlineData("ALTER TABLE mytable ALTER COLUMN my_val SET DEFAULT 10", null)]
    [InlineData("ALTER TABLE mytable ALTER COLUMN my_val SET DEFAULT 'abc'", null)]
    [InlineData("ALTER TABLE mytable ALTER COLUMN my_val SET DEFAULT (a + b)", null)]
    [InlineData("ALTER TABLE mytable ALTER COLUMN my_val DROP DEFAULT", null)]
    /* Change/Modify Columns */
    [InlineData("ALTER TABLE mytable MODIFY COLUMN my_val VARCHAR(50) NOT NULL", null)]
    [InlineData("ALTER TABLE mytable CHANGE COLUMN my_val my_new_val VARCHAR(100) NULL", null)]
    [InlineData("ALTER TABLE mytable CHANGE COLUMN my_val my_val VARCHAR(100) NULL", null)]
    [InlineData("ALTER TABLE mytable MODIFY my_val VARCHAR(50) NOT NULL", "ALTER TABLE mytable MODIFY COLUMN my_val VARCHAR(50) NOT NULL")]
    [InlineData("ALTER TABLE mytable CHANGE my_val my_new_val VARCHAR(100) NULL", "ALTER TABLE mytable CHANGE COLUMN my_val my_new_val VARCHAR(100) NULL")]
    [InlineData("ALTER TABLE mytable MODIFY COLUMN my_val INT NOT NULL AFTER other_col", null)]
    [InlineData("ALTER TABLE mytable MODIFY COLUMN my_val INT NOT NULL FIRST", null)]
    [InlineData("ALTER TABLE mytable CHANGE COLUMN my_val my_new_val INT NOT NULL AFTER other_col", null)]
    [InlineData("ALTER TABLE mytable CHANGE COLUMN my_val my_new_val INT NOT NULL FIRST", null)]
    /* Add Keys */
    [InlineData("ALTER TABLE mytable ADD PRIMARY KEY (my_id)", null)]
    [InlineData("ALTER TABLE mytable ADD CONSTRAINT uidx_mytable_my_val UNIQUE (my_val)", null)]
    [InlineData("ALTER TABLE mytable ADD CONSTRAINT uidx_mytable_my_val UNIQUE KEY (my_val)", null)]
    [InlineData("ALTER TABLE mytable ADD CONSTRAINT uidx_mytable_my_val UNIQUE INDEX (my_val)", null)]
    [InlineData("ALTER TABLE mytable ADD UNIQUE (my_val)", null)]
    [InlineData("ALTER TABLE mytable ADD UNIQUE KEY (my_val)", null)]
    [InlineData("ALTER TABLE mytable ADD UNIQUE INDEX (my_val)", null)]
    [InlineData("ALTER TABLE mytable ADD UNIQUE KEY (my_val1, my_val2)", null)]
    [InlineData("ALTER TABLE mytable ADD UNIQUE KEY uidx_mytable_my_val (my_val)", null)]
    [InlineData("ALTER TABLE mytable ADD KEY idx_mytable_myval (my_val)", null)]
    [InlineData("ALTER TABLE mytable ADD INDEX idx_mytable_myval (my_val)", null)]
    [InlineData("ALTER TABLE mytable ADD INDEX idx_mytable_myval USING BTREE (my_val)", null)]
    [InlineData("ALTER TABLE mytable ADD FULLTEXT INDEX idx_mytable_myval (my_val)", null)]
    [InlineData("ALTER TABLE mytable ADD SPATIAL INDEX idx_mytable_myval (my_val)", null)]
    /* Add Foreign Key */
    [InlineData("ALTER TABLE mytable ADD FOREIGN KEY (other_id) REFERENCES othertable (other_id)", null)]
    [InlineData("ALTER TABLE mytable ADD CONSTRAINT fk_mytable_othertable FOREIGN KEY (other_id) REFERENCES othertable (other_id)", null)]
    /* Add Check */
    [InlineData("ALTER TABLE mytable ADD CHECK (my_val > 0)", null)]
    [InlineData("ALTER TABLE mytable ADD CONSTRAINT ck_mytable_nonneg CHECK (my_val > 0)", null)]
    /* Drops */
    [InlineData("ALTER TABLE mytable DROP COLUMN old_col", null)]
    [InlineData("ALTER TABLE mytable DROP old_col", "ALTER TABLE mytable DROP COLUMN old_col")]
    [InlineData("ALTER TABLE mytable DROP PRIMARY KEY pk_name", null)]
    [InlineData("ALTER TABLE mytable DROP FOREIGN KEY fk_mytable_othertable", null)]
    [InlineData("ALTER TABLE mytable DROP INDEX my_index", "ALTER TABLE mytable DROP KEY my_index")]
    [InlineData("ALTER TABLE mytable DROP KEY my_key", null)]
    [InlineData("ALTER TABLE mytable DROP PRIMARY KEY", null)]
    [InlineData("ALTER TABLE mytable DROP CHECK ck_mytable_nonneg", "ALTER TABLE mytable DROP CONSTRAINT ck_mytable_nonneg")]
    [InlineData("ALTER TABLE mytable DROP CONSTRAINT ck_mytable_nonneg", null)]
    /* Table Renames */
    [InlineData("ALTER TABLE mytable RENAME TO new_table_name", null)]
    [InlineData("ALTER TABLE mytable RENAME AS new_table_name", "ALTER TABLE mytable RENAME TO new_table_name")]
    [InlineData("ALTER TABLE mytable RENAME new_table_name", "ALTER TABLE mytable RENAME TO new_table_name")]
    /* Column Renames */
    [InlineData("ALTER TABLE mytable RENAME COLUMN old_col TO new_col", null)]
    /* Table Options */
    [InlineData("ALTER TABLE mytable AUTO_INCREMENT = 100", null)]
    /* Multiple Operations */
    [InlineData("ALTER TABLE mytable ADD COLUMN new_col INT, ADD PRIMARY KEY (my_id)", null)]
    [InlineData("ALTER TABLE mytable ADD COLUMN new_col INT, ADD PRIMARY KEY (my_id), DROP CONSTRAINT ck_mytable_nonneg", null, Label = "Multiple operations with drop")]
    public static void Alter_Table_TextMatch(string sql, string? realExpected)
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
    public static void Alter_Table_Other_Throws(string sql)
    {
        var parser = new Parser();

        var exception = Assert.Throws<ParseException.ExpectedButFound>(() => parser.Parse(GetTokens(sql)));
        var expectedTemplate = "Expected {0}. Found {1}.";
        var expected = string.Format(expectedTemplate, "alter_table_operation".Italic(), "DOUNKNOWNTHING");
        Assert.Equal(expected, exception.Message);
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
