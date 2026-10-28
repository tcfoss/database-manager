using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.MySql.Lexing;
using TcfOss.DatabaseManager.MySql.Parsing;

namespace TcfOss.DatabaseManager.MySql.Tests.ParserTests;

public class MyTableParserTests : ParserTestsBase<MyLexer, MyParser>
{
    [Theory]
    [InlineData("CREATE TABLE mytable (id INT NOT NULL PRIMARY KEY)", Label = "primary key")]
    [InlineData("CREATE TABLE mytable (id INT NOT NULL, last_update TIMESTAMP DEFAULT (CURRENT_TIMESTAMP()) ON UPDATE CURRENT_TIMESTAMP())", Label = "timestamp on update")]
    [InlineData("CREATE TABLE mytable (id INT, name VARCHAR(10)) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci", Label = "engine equals charset collation")]
    [InlineData("CREATE TABLE mytable (id INT, name VARCHAR(10)) ENGINE InnoDB DEFAULT CHARSET utf8mb4 COLLATE utf8mb4_0900_ai_ci", Label = "engine without equals")]
    [InlineData("CREATE TABLE mytable (id INT, name VARCHAR(10)) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 DEFAULT COLLATE = utf8mb4_0900_ai_ci", Label = "default charset and collation")]
    [InlineData("CREATE TABLE mytable (id INT, name VARCHAR(10)) ENGINE = InnoDB DEFAULT CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci", Label = "character set keyword")]
    [InlineData("CREATE TABLE mytable (id INT, name VARCHAR(10)) ENGINE InnoDB DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci", Label = "engine and character set without equals")]
    [InlineData("CREATE TABLE mytable (id INT, name VARCHAR(10)) ENGINE = InnoDB DEFAULT CHARACTER SET = utf8mb4 DEFAULT COLLATE = utf8mb4_0900_ai_ci", Label = "character set and default collation")]
    [InlineData("CREATE TABLE mytable (id INT PRIMARY KEY AUTO_INCREMENT, name VARCHAR(10)) STORAGE ENGINE = InnoDB DEFAULT CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci", Label = "storage engine with charset")]
    [InlineData("CREATE TABLE mytable (id INT PRIMARY KEY AUTO_INCREMENT, name VARCHAR(10)) AUTO_INCREMENT = 1000", Label = "auto increment equals")]
    [InlineData("CREATE TABLE mytable (id INT PRIMARY KEY AUTO_INCREMENT, name VARCHAR(10)) AUTO_INCREMENT 1000", Label = "auto increment without equals")]
    [InlineData("CREATE TABLE mytable (id INT, name VARCHAR(10)) COMMENT 'My table comment'", Label = "table comment")]
    [InlineData("CREATE TABLE mytable (id INT, name VARCHAR(10)) COMMENT = 'My table comment'", Label = "table comment equals")]
    public void Create_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text);
        Assert.Equal(expected, actual);
    }

    // Optional Text Length for Text Types
    [Theory]
    [InlineData("CREATE TABLE mytable (id INT, description TEXT(65535))", Label = "text length")]
    [InlineData("CREATE TABLE mytable (id INT, description TINYTEXT(255))", Label = "tinytext length")]
    [InlineData("CREATE TABLE mytable (id INT, description MEDIUMTEXT(16777215))", Label = "mediumtext length")]
    [InlineData("CREATE TABLE mytable (id INT, description LONGTEXT(4294967295))", Label = "longtext length")]
    public void Create_TextLength_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text);
        Assert.Equal(expected, actual);
    }

    // Spatial Columns and Indexes
    [Theory]
    [InlineData("CREATE TABLE mytable (id INT PRIMARY KEY, location POINT NOT NULL, SPATIAL INDEX idx_location (location))", Label = "spatial index")]
    [InlineData("CREATE TABLE mytable (id INT PRIMARY KEY, location POINT NOT NULL, SPATIAL INDEX idx_location (location) USING RTREE)", Label = "spatial index rtree")]
    [InlineData("CREATE TABLE mytable (id INT PRIMARY KEY, area POLYGON, SPATIAL INDEX idx_area (area) COMMENT 'Area index')", Label = "spatial index comment")]
    public void Create_SpatialColumnsAndIndexes_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text);
        Assert.Equal(expected, actual);
    }

    // Create Table As Select With Options
    [Theory]
    [InlineData("CREATE TABLE mytable (new_id INT, new_val VARCHAR(20)) ENGINE = InnoDB DEFAULT CHARSET utf8mb4 AS SELECT my_id, my_val FROM othertable", Label = "as select with engine and charset")]
    public void Create_AsSelectWithOptions_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text);
        Assert.Equal(expected, actual);
    }

    // Auto-Increment
    [Theory]
    [InlineData("CREATE TABLE mytable (id INT PRIMARY KEY AUTO_INCREMENT, name VARCHAR(10), PRIMARY KEY (id)) AUTO_INCREMENT = 5000", Label = "auto increment table option")]
    [InlineData("CREATE TABLE mytable (id INT PRIMARY KEY AUTO_INCREMENT, name VARCHAR(10), PRIMARY KEY (id)) DEFAULT CHARACTER SET utf8mb4 AUTO_INCREMENT = 5000", Label = "charset with auto increment")]
    public void Create_AutoIncrement_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public static void CreateTable_Column_SymbolDataType_Throws()
    {
        var sql = "CREATE TABLE mytable (my_id INT NOT NULL, my_val +)";
        var parser = new MyParser();
        var ex = Assert.Throws<ParseException.ExpectedButFound>(() => parser.Parse(GetTokens(sql)));
        Assert.Contains("+", ex.Message);
        Assert.Contains("data_type", ex.Message);
    }

    [Theory]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 ENGINE = MyISAM", Label = "duplicate ENGINE/CHARSET options")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL) DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci DEFAULT CHARSET = latin1", Label = "duplicate CHARSET/COLLATE options")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL) COLLATE = utf8mb4_0900_ai_ci COLLATE = latin1_swedish_ci", Label = "duplicate COLLATE options")]
    public void CreateTable_DuplicateTableOptions_Throws(string sql)
    {
        var parser = new MyParser();
        Assert.Throws<ParseException.Duplicate>(() => parser.Parse(GetTokens(sql)));
    }

    [Fact]
    public void CreateTable_Index_UnknownMethod_Throws()
    {
        var sql = "CREATE TABLE mytable (id INT PRIMARY KEY, location POINT NOT NULL, SPATIAL INDEX idx_location (location) USING UNKNOWN)";
        var parser = new MyParser();
        var ex = Assert.Throws<ParseException.ExpectedOneOfButFound>(() => parser.Parse(GetTokens(sql)));
        Assert.Contains("BTREE", ex.Message);
        Assert.Contains("RTREE", ex.Message);
    }

    [Fact]
    public void CreateTable_TableUnique_NoColumns_Throws()
    {
        var sql = "CREATE TABLE mytable (CONSTRAINT uq_mytable UNIQUE)";
        var parser = new MyParser();
        var ex = Assert.Throws<ParseException.ExpectedButFound>(() => parser.Parse(GetTokens(sql)));
        Assert.Contains("column_list", ex.Message);
    }

    [Theory]
    [InlineData("myindex", "myindex")]
    [InlineData("`SELECT`", "SELECT")]
    public void CreateTable_Unique_DeterminesIndexName(string name, string expectedName)
    {
        var sql = $"CREATE TABLE mytable (UNIQUE KEY {name} (mycol))";

        var parser = new MyParser();
        var statements = parser.Parse(GetTokens(sql));

        var createTable = Assert.IsType<CreateTable>(Assert.Single(statements));
        var uniqueKey = Assert.IsType<StatementTableConstraint.UniqueConstraint>(Assert.Single(createTable.Constraints));

        Assert.Equal(expectedName, uniqueKey.IndexName?.Name);
    }

    [Fact]
    public void CreateTable_Unique_KeywordInNamePosition_Throws()
    {
        var sql = "CREATE TABLE mytable (UNIQUE KEY SELECT (mycol))";
        var parser = new MyParser();
        var ex = Assert.Throws<ParseException.ExpectedButFound>(() => parser.Parse(GetTokens(sql)));
        Assert.Contains("column_list", ex.Message);
    }

    [Fact]
    public void MsComputedColumn_Throws()
    {
        var sql = "CREATE TABLE mytable (x AS (y + 1) PERSISTED)";
        var parser = new MyParser();
        var ex = Assert.Throws<ParseException.ExpectedButFound>(() => parser.Parse(GetTokens(sql)));
        Assert.Contains("data_type", ex.Message);
    }

    [Fact]
    public void AlterTable_Default_InvalidFollowup_Throws()
    {
        var sql = "ALTER TABLE mytable DEFAULT CAT;";
        var parser = new MyParser();
        var ex = Assert.Throws<ParseException.ExpectedOneOfButFound>(() => parser.Parse(GetTokens(sql)));
        Assert.Contains("CHARACTER SET", ex.Message);
        Assert.Contains("COLLATE", ex.Message);
    }
}

