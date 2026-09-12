using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.MySql.Lexing;
using TcfOss.DatabaseManager.MySql.Parsing;

namespace TcfOss.DatabaseManager.MySql.Tests.ParserTests;

public class MyTableParserTests : ParserTestsBase<MyLexer, MyParser>
{
    [Theory]
    [InlineData("CREATE TABLE mytable (id INT NOT NULL PRIMARY KEY)")]
    [InlineData("CREATE TABLE mytable (id INT NOT NULL, last_update TIMESTAMP DEFAULT (CURRENT_TIMESTAMP()) ON UPDATE CURRENT_TIMESTAMP())")]
    [InlineData("CREATE TABLE mytable (id INT, name VARCHAR(10)) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci", Label = "ENGINE=InnoDB DEFAULT CHARSET utf8mb4 COLLATE utf8mb4_0900_ai_ci")]
    [InlineData("CREATE TABLE mytable (id INT, name VARCHAR(10)) ENGINE InnoDB DEFAULT CHARSET utf8mb4 COLLATE utf8mb4_0900_ai_ci", Label = "ENGINE InnoDB DEFAULT CHARSET utf8mb4 COLLATE utf8mb4_0900_ai_ci")]
    [InlineData("CREATE TABLE mytable (id INT, name VARCHAR(10)) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 DEFAULT COLLATE = utf8mb4_0900_ai_ci", Label = "ENGINE=InnoDB DEFAULT CHARSET utf8mb4 DEFAULT COLLATE utf8mb4_0900_ai_ci")]
    [InlineData("CREATE TABLE mytable (id INT, name VARCHAR(10)) ENGINE = InnoDB DEFAULT CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci", Label = "ENGINE=InnoDB DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci")]
    [InlineData("CREATE TABLE mytable (id INT, name VARCHAR(10)) ENGINE InnoDB DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci", Label = "ENGINE InnoDB DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci")]
    [InlineData("CREATE TABLE mytable (id INT, name VARCHAR(10)) ENGINE = InnoDB DEFAULT CHARACTER SET = utf8mb4 DEFAULT COLLATE = utf8mb4_0900_ai_ci", Label = "ENGINE=InnoDB DEFAULT CHARACTER SET utf8mb4 DEFAULT COLLATE utf8mb4_0900_ai_ci")]
    [InlineData("CREATE TABLE mytable (id INT PRIMARY KEY AUTO_INCREMENT, name VARCHAR(10)) STORAGE ENGINE = InnoDB DEFAULT CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci", Label = "STORAGE ENGINE=InnoDB DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci")]
    [InlineData("CREATE TABLE mytable (id INT PRIMARY KEY AUTO_INCREMENT, name VARCHAR(10)) AUTO_INCREMENT = 1000")]
    [InlineData("CREATE TABLE mytable (id INT PRIMARY KEY AUTO_INCREMENT, name VARCHAR(10)) AUTO_INCREMENT 1000")]
    [InlineData("CREATE TABLE mytable (id INT, name VARCHAR(10)) COMMENT 'My table comment'")]
    [InlineData("CREATE TABLE mytable (id INT, name VARCHAR(10)) COMMENT = 'My table comment'")]
    // Optional Text Length for Text Types
    [InlineData("CREATE TABLE mytable (id INT, description TEXT(65535))")]
    [InlineData("CREATE TABLE mytable (id INT, description TINYTEXT(255))")]
    [InlineData("CREATE TABLE mytable (id INT, description MEDIUMTEXT(16777215))")]
    [InlineData("CREATE TABLE mytable (id INT, description LONGTEXT(4294967295))")]
    // Spatial Columns and Indexes
    [InlineData("CREATE TABLE mytable (id INT PRIMARY KEY, location POINT NOT NULL, SPATIAL INDEX idx_location (location))")]
    [InlineData("CREATE TABLE mytable (id INT PRIMARY KEY, area POLYGON, SPATIAL INDEX idx_area (area) COMMENT 'Area index')")]
    // Create Table As Select With Options
    [InlineData("CREATE TABLE mytable (new_id INT, new_val VARCHAR(20)) ENGINE = InnoDB DEFAULT CHARSET utf8mb4 AS SELECT my_id, my_val FROM othertable", Label = "CREATE ... AS SELECT with ENGINE/CHARSET")]
    // Auto-Increment
    [InlineData("CREATE TABLE mytable (id INT PRIMARY KEY AUTO_INCREMENT, name VARCHAR(10), PRIMARY KEY (id)) AUTO_INCREMENT = 5000")]
    [InlineData("CREATE TABLE mytable (id INT PRIMARY KEY AUTO_INCREMENT, name VARCHAR(10), PRIMARY KEY (id)) DEFAULT CHARACTER SET utf8mb4 AUTO_INCREMENT = 5000", Label = "DEFAULT CHARACTER SET utf8mb4 with AUTO_INCREMENT")]
    public void Create_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 ENGINE = MyISAM", Label = "duplicate ENGINE/CHARSET options")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL) DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci DEFAULT CHARSET = latin1", Label = "duplicate CHARSET/COLLATE options")]
    [InlineData("CREATE TABLE mytable (my_id INT NOT NULL) COLLATE = utf8mb4_0900_ai_ci COLLATE = latin1_swedish_ci", Label = "duplicate COLLATE options")]
    public void CreateTable_DuplicateTableOptions_Throws(string sql)
    {
        var lexer = new MyLexer();
        var parser = new MyParser();
        var tokens = lexer.Tokenize(sql);
        Assert.Throws<ParseException.Duplicate>(() => parser.Parse([.. tokens]));
    }
}

