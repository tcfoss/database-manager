using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.MySql.Lexing;
using TcfOss.DatabaseManager.MySql.Parsing;

namespace TcfOss.DatabaseManager.MySql.Tests.ParserTests;

public class MyParserTests : ParserTestsBase<MyLexer, MyParser>
{
    [Theory]
    /* IF block */
    [InlineData("IF a > b THEN SELECT 1; END IF")]
    [InlineData("IF a > b THEN SELECT 1; SELECT 2; END IF")]
    [InlineData("IF a > b THEN SELECT 1; ELSE SELECT 2; END IF")]
    [InlineData("IF a > b THEN SELECT 1; ELSE SELECT 2; SELECT 3; END IF")]
    [InlineData("IF a > b THEN SELECT 1; ELSEIF c > d THEN SELECT 2; SELECT 3; ELSE SELECT 4; END IF")]
    [InlineData("IF a > b THEN SELECT 1; ELSEIF c > d THEN SELECT 2; ELSEIF e > f THEN SELECT 3; END IF")]
    [InlineData("IF a > b THEN SELECT 1; ELSEIF c > d THEN SELECT 2; ELSEIF e > f THEN SELECT 3; ELSEIF g > h THEN SELECT 4; END IF")]
    [InlineData("IF a > b THEN SELECT 1; ELSEIF c > d THEN SELECT 2; ELSEIF e > f THEN SELECT 3; ELSEIF g > h THEN SELECT 4; ELSE SELECT 5; END IF")]
    /* DECLARE local variable */
    [InlineData("DECLARE myvar INT")]
    [InlineData("DECLARE myvar VARCHAR(10)")]
    [InlineData("DECLARE myvar DECIMAL(10,2) DEFAULT 0.5")]
    public void Statement_TextMatch(string sql)
    {
        var (expected, actual) = GetExpectedActual(sql, expectedSql: sql, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'An error occurred.'", Label = "SIGNAL basic")]
    [InlineData("SIGNAL SQLSTATE 'HY000' SET MYSQL_ERRNO = 1644, MESSAGE_TEXT = 'Custom error message.'", Label = "SIGNAL mysql errno")]
    [InlineData("SIGNAL SQLSTATE '42000' SET MESSAGE_TEXT = 'Syntax error.', MYSQL_ERRNO = 1064", Label = "SIGNAL syntax errno")]
    [InlineData("SIGNAL SQLSTATE '50000' SET CLASS_ORIGIN = 'MyClass', SUBCLASS_ORIGIN = 'MySubClass', MESSAGE_TEXT = 'Detailed error message.', MYSQL_ERRNO = 1644", Label = "SIGNAL class origin")]
    [InlineData("SIGNAL SQLSTATE '50001' SET CONSTRAINT_CATALOG = 'MyCatalog', CONSTRAINT_SCHEMA = 'MySchema', CONSTRAINT_NAME = 'MyConstraint', MESSAGE_TEXT = 'Constraint violation.', MYSQL_ERRNO = 1644", Label = "SIGNAL constraint meta")]
    [InlineData("SIGNAL SQLSTATE '50002' SET CATALOG_NAME = 'MyCatalog', SCHEMA_NAME = 'MySchema', TABLE_NAME = 'MyTable', COLUMN_NAME = 'MyColumn', MESSAGE_TEXT = 'Table/Column error.', MYSQL_ERRNO = 1644", Label = "SIGNAL catalog schema table column")]
    [InlineData("SIGNAL SQLSTATE '50003' SET CURSOR_NAME = 'MyCursor', MESSAGE_TEXT = 'Cursor error.', MYSQL_ERRNO = 1644", Label = "SIGNAL cursor")]
    [InlineData("SIGNAL declared_condition SET MESSAGE_TEXT = 'Declared condition error.'", Label = "SIGNAL declared condition")]
    [InlineData("SIGNAL SQLSTATE '45000'", Label = "SIGNAL sqlstate only")]
    public void Signal_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("SIGNAL SQLSTATE '45000' SET MESSAGE_TXT = 'An error occurred.'", "signal_property_name", "MESSAGE_TXT")]
    [InlineData("SIGNAL SQLSTATE '45000' SET +", "signal_property_name", "+")]
    [InlineData("SIGNAL SQLSTATE NOTLIT", "sqlstate_value", "NOTLIT")]
    [InlineData("SIGNAL 'AAAH'", "condition_name", "'AAAH'")]
    public void Signal_Invalid_Throws(string text, string expectedType, string found)
    {
        var exception = Assert.Throws<ParseException.ExpectedButFound>(() => ParseStatement(text));
        var expectedTemplate = "Expected {0}. Found {1}.";
        var expected = string.Format(expectedTemplate, expectedType.Italic(), found);
        Assert.Equal(expected, exception.Message);
    }

    [Theory]
    [InlineData("RESIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'An error occurred.'", Label = "RESIGNAL basic")]
    [InlineData("RESIGNAL SQLSTATE 'HY000' SET MYSQL_ERRNO = 1644, MESSAGE_TEXT = 'Custom error message.'", Label = "RESIGNAL mysql errno")]
    [InlineData("RESIGNAL SQLSTATE '42000' SET MESSAGE_TEXT = 'Syntax error.', MYSQL_ERRNO = 1064", Label = "RESIGNAL syntax errno")]
    [InlineData("RESIGNAL SQLSTATE '50000' SET CLASS_ORIGIN = 'MyClass', SUBCLASS_ORIGIN = 'MySubClass', MESSAGE_TEXT = 'Detailed error message.', MYSQL_ERRNO = 1644", Label = "RESIGNAL class origin")]
    [InlineData("RESIGNAL SQLSTATE '50001' SET CONSTRAINT_CATALOG = 'MyCatalog', CONSTRAINT_SCHEMA = 'MySchema', CONSTRAINT_NAME = 'MyConstraint', MESSAGE_TEXT = 'Constraint violation.', MYSQL_ERRNO = 1644", Label = "RESIGNAL constraint meta")]
    [InlineData("RESIGNAL SQLSTATE '50002' SET CATALOG_NAME = 'MyCatalog', SCHEMA_NAME = 'MySchema', TABLE_NAME = 'MyTable', COLUMN_NAME = 'MyColumn', MESSAGE_TEXT = 'Table/Column error.', MYSQL_ERRNO = 1644", Label = "RESIGNAL catalog schema table column")]
    [InlineData("RESIGNAL SQLSTATE '50003' SET CURSOR_NAME = 'MyCursor', MESSAGE_TEXT = 'Cursor error.', MYSQL_ERRNO = 1644", Label = "RESIGNAL cursor")]
    [InlineData("RESIGNAL SQLSTATE VALUE '45000' SET MESSAGE_TEXT = 'An error occurred.'", Label = "RESIGNAL sqlstate value")]
    [InlineData("RESIGNAL declared_condition SET MESSAGE_TEXT = 'Declared condition error.'", Label = "RESIGNAL declared condition")]
    [InlineData("RESIGNAL SQLSTATE '45000'", Label = "RESIGNAL sqlstate only")]
    [InlineData("RESIGNAL", Label = "RESIGNAL keyword only")]
    public void Resignal_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("RESIGNAL SQLSTATE 45000", "sqlstate_value", "45000")]
    public void Resignal_Invalid_Throws(string text, string expectedType, string found)
    {
        var exception = Assert.Throws<ParseException.ExpectedButFound>(() => ParseStatement(text));
        var expectedTemplate = "Expected {0}. Found {1}.";
        var expected = string.Format(expectedTemplate, expectedType.Italic(), found);
        Assert.Equal(expected, exception.Message);
    }


    [Theory]
    [InlineData("DECLARE my_condition CONDITION FOR SQLSTATE '45000'")]
    [InlineData("DECLARE my_condition CONDITION FOR SQLSTATE VALUE '45000'")]
    [InlineData("DECLARE my_condition CONDITION FOR 1644")]
    public void DeclareCondition_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("LOOP SELECT 1; END LOOP")]
    [InlineData("looplabel: LOOP SELECT 'test'; END LOOP looplabel")]
    [InlineData("myLoop: LOOP SET p1 = p1 + 1; IF p1 < 10 THEN ITERATE myLoop; END IF; LEAVE myLoop; END LOOP myLoop")]
    public void Loop_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("PREPARE stmt1 FROM 'SELECT * FROM my_table WHERE id = ?'", Label = "PREPARE select")]
    [InlineData("PREPARE stmt2 FROM 'INSERT INTO my_table (name, value) VALUES (?, ?)'", Label = "PREPARE insert")]
    [InlineData("PREPARE stmt3 FROM @myvar", Label = "PREPARE from variable")]
    [InlineData("PREPARE stmt4 FROM myparam", Label = "PREPARE from param")]
    [InlineData("EXECUTE stmt1", Label = "EXECUTE simple")]
    [InlineData("EXECUTE stmt2 USING @param1, @param2", Label = "EXECUTE using params")]
    [InlineData("DEALLOCATE PREPARE stmt1", Label = "DEALLOCATE PREPARE")]
    [InlineData("DROP PREPARE stmt2", Label = "DROP PREPARE")]
    [InlineData("DROP TABLE IF EXISTS mytable", Label = "DROP TABLE fallback")] // Make sure it falls back to default parser
    public void PreparedStatements_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("SHOW ERRORS")]
    [InlineData("SHOW WARNINGS")]
    [InlineData("SHOW COUNT(*) ERRORS")]
    [InlineData("SHOW COUNT(*) WARNINGS")]
    [InlineData("SHOW ERRORS LIMIT 10")]
    [InlineData("SHOW WARNINGS LIMIT 5, 15")]
    public void ShowDiagnostics_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("START TRANSACTION")]
    [InlineData("START TRANSACTION WITH CONSISTENT SNAPSHOT")]
    [InlineData("START TRANSACTION READ ONLY")]
    [InlineData("START TRANSACTION READ WRITE")]
    [InlineData("COMMIT")]
    [InlineData("COMMIT AND CHAIN")]
    [InlineData("COMMIT AND NO CHAIN")]
    [InlineData("COMMIT RELEASE")]
    [InlineData("COMMIT NO RELEASE")]
    [InlineData("COMMIT AND CHAIN RELEASE")]
    [InlineData("COMMIT AND CHAIN NO RELEASE")]
    [InlineData("COMMIT AND NO CHAIN RELEASE")]
    [InlineData("COMMIT AND NO CHAIN NO RELEASE")]
    [InlineData("COMMIT WORK")]
    [InlineData("COMMIT WORK AND CHAIN")]
    [InlineData("COMMIT WORK AND NO CHAIN")]
    [InlineData("COMMIT WORK RELEASE")]
    [InlineData("COMMIT WORK NO RELEASE")]
    [InlineData("COMMIT WORK AND CHAIN RELEASE")]
    [InlineData("COMMIT WORK AND CHAIN NO RELEASE")]
    [InlineData("COMMIT WORK AND NO CHAIN RELEASE")]
    [InlineData("COMMIT WORK AND NO CHAIN NO RELEASE")]
    [InlineData("ROLLBACK")]
    [InlineData("ROLLBACK AND CHAIN")]
    [InlineData("ROLLBACK AND NO CHAIN")]
    [InlineData("ROLLBACK RELEASE")]
    [InlineData("ROLLBACK NO RELEASE")]
    [InlineData("ROLLBACK AND CHAIN RELEASE")]
    [InlineData("ROLLBACK AND CHAIN NO RELEASE")]
    [InlineData("ROLLBACK AND NO CHAIN RELEASE")]
    [InlineData("ROLLBACK AND NO CHAIN NO RELEASE")]
    [InlineData("ROLLBACK WORK")]
    [InlineData("ROLLBACK WORK AND CHAIN")]
    [InlineData("ROLLBACK WORK AND NO CHAIN")]
    [InlineData("ROLLBACK WORK RELEASE")]
    [InlineData("ROLLBACK WORK NO RELEASE")]
    [InlineData("ROLLBACK WORK AND CHAIN RELEASE")]
    [InlineData("ROLLBACK WORK AND CHAIN NO RELEASE")]
    [InlineData("ROLLBACK WORK AND NO CHAIN RELEASE")]
    [InlineData("ROLLBACK WORK AND NO CHAIN NO RELEASE")]
    public void Transactions_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("START TRANSACTION WITH CONSISTENT SNAPSHOT READ BLAH", "{ ONLY | WRITE }", "BLAH")]
    public void Transactions_Begin_Invalid_Throws(string text, string expectedText, string found)
    {
        var exception = Assert.Throws<ParseException.ExpectedOneOfButFound>(() => ParseStatement(text));
        var expectedTemplate = "Expected one of {0}. Found {1}.";
        var expected = string.Format(expectedTemplate, expectedText, found);
        Assert.Equal(expected, exception.Message);
    }

    [Theory]
    [InlineData("FETCH cursor1 INTO @var1")]
    [InlineData("FETCH cursor2 INTO var1")]
    [InlineData("FETCH cursor3 INTO @var1, @var2, @var3")]
    [InlineData("FETCH cursor4 INTO var1, var2, var3")]
    [InlineData("FETCH FROM cursor5 INTO @var1, @var2, @var3")]
    [InlineData("FETCH NEXT FROM cursor6 INTO var1, var2, var3")]
    [InlineData("FETCH GROUP NEXT ROW")]
    public void Fetch_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("OPEN cursor1")]
    [InlineData("CLOSE cursor2")]
    public void OpenCloseCursor_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("SELECT * FROM my_table WHERE MATCH (col1, col2) AGAINST ('search text')", Label = "SELECT MATCH AGAINST basic")]
    [InlineData("SELECT * FROM my_table WHERE MATCH (col1, col2) AGAINST ('search text' IN NATURAL LANGUAGE MODE)", Label = "SELECT MATCH AGAINST NATURAL LANGUAGE MODE")]
    [InlineData("SELECT * FROM my_table WHERE MATCH (col1, col2) AGAINST ('search text' WITH QUERY EXPANSION)", Label = "SELECT MATCH AGAINST QUERY EXPANSION")]
    [InlineData("SELECT * FROM my_table WHERE MATCH (col1, col2) AGAINST ('search text' IN NATURAL LANGUAGE MODE WITH QUERY EXPANSION)", Label = "SELECT MATCH AGAINST NATURAL LANGUAGE WITH QUERY EXPANSION")]
    [InlineData("SELECT * FROM my_table WHERE MATCH (col1, col2) AGAINST ('search text' IN BOOLEAN MODE)", Label = "SELECT MATCH AGAINST BOOLEAN MODE")]
    public void SelectMatchAgainst_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("CALL my_stored_procedure()")]
    [InlineData("CALL my_stored_procedure(`param1`)")]
    [InlineData("CALL my_stored_procedure(param1, param2)")]
    [InlineData("CALL `my_stored_procedure`(@param1, @param2, @param3)")]
    public void CallStatement_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("CONVERT(col1 USING utf8mb4)", Label = "CONVERT USING")]
    [InlineData("CONVERT(col2, CHAR CHARACTER SET utf8mb4)", Label = "CONVERT CHAR")]
    [InlineData("CONVERT(col3, CHAR(10) CHARACTER SET utf8mb4)", Label = "CONVERT CHAR(10)")]
    [InlineData("CONVERT(col4, VARCHAR CHARACTER SET utf8mb4)", Label = "CONVERT VARCHAR")]
    [InlineData("CONVERT(col5, VARCHAR(255) CHARACTER SET utf8mb4)", Label = "CONVERT VARCHAR(255)")]
    [InlineData("CONVERT(col6, NATIONAL VARCHAR CHARACTER SET utf8mb4)", Label = "CONVERT NATIONAL VARCHAR")]
    [InlineData("CONVERT(col7, NATIONAL VARCHAR(100) CHARACTER SET utf8mb4)", Label = "CONVERT NATIONAL VARCHAR(100)")]
    [InlineData("CONVERT(col8, TEXT CHARACTER SET utf8mb4)", Label = "CONVERT TEXT")]
    [InlineData("CONVERT(col9, INT)", Label = "CONVERT INT")]
    [InlineData("CAST(col10 AS CHAR(1000))", Label = "CAST AS CHAR(1000)")]
    [InlineData("CAST(col11 AS CHAR(1000) CHARACTER SET utf8mb4)", Label = "CAST AS CHAR(1000) CHARACTER SET utf8mb4")]
    public void Expressions_TextMatch(string text)
    {
        var parser = new MyParser();
        var (expected, actual) = GetExpectedActual(parser.ExpressionParser.ParseExpr, text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }
}
