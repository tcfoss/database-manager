using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.MySql.Lexing;
using TcfOss.DatabaseManager.MySql.Parsing;

namespace TcfOss.DatabaseManager.MySql.Tests.ParserTests;

public class MyParserTests : ParserTestsBase<MyLexer, MyParser>
{
    [Theory]
    [InlineData("IF a > b THEN SELECT 1; END IF")]
    [InlineData("IF a > b THEN SELECT 1; SELECT 2; END IF")]
    [InlineData("IF a > b THEN SELECT 1; ELSE SELECT 2; END IF")]
    [InlineData("IF a > b THEN SELECT 1; ELSE SELECT 2; SELECT 3; END IF")]
    [InlineData("IF a > b THEN SELECT 1; ELSEIF c > d THEN SELECT 2; SELECT 3; ELSE SELECT 4; END IF")]
    [InlineData("IF a > b THEN SELECT 1; ELSEIF c > d THEN SELECT 2; ELSEIF e > f THEN SELECT 3; END IF")]
    [InlineData("IF a > b THEN SELECT 1; ELSEIF c > d THEN SELECT 2; ELSEIF e > f THEN SELECT 3; ELSEIF g > h THEN SELECT 4; END IF")]
    [InlineData("IF a > b THEN SELECT 1; ELSEIF c > d THEN SELECT 2; ELSEIF e > f THEN SELECT 3; ELSEIF g > h THEN SELECT 4; ELSE SELECT 5; END IF")]
    [InlineData("IF a > b THEN SELECT 1; ELSEIF c > d THEN SELECT 2; END IF")]
    public void If_TextMatch(string sql)
    {
        var (expected, actual) = GetExpectedActual(sql, expectedSql: sql, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("DECLARE myvar INT")]
    [InlineData("DECLARE myvar VARCHAR(10)")]
    [InlineData("DECLARE myvar DECIMAL(10,2) DEFAULT 0.5")]
    public void DeclareLocalVariable_TextMatch(string sql)
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

    [Fact]
    public void ShowDiagnostics_Unknown_Throws()
    {
        var ex = Assert.Throws<ParseException.ExpectedOneOfButFound>(() => ParseStatements("SHOW UNKNOWN"));
        Assert.Contains("{ ERRORS | WARNINGS | COUNT }", ex.Message);
    }

    [Fact]
    public void ShowDiagnostics_WithCount_NotErrorsOrWarnings_Throws()
    {
        var ex = Assert.Throws<ParseException.ExpectedOneOfButFound>(() => ParseStatements("SHOW COUNT(*) UNKNOWN"));
        Assert.Contains("{ ERRORS | WARNINGS }", ex.Message);
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
    [InlineData("SELECT a INTO result FROM source")]
    [InlineData("SELECT a, b INTO result FROM source")]
    [InlineData("SELECT a FROM source INTO result")]
    [InlineData("SELECT a, b FROM source INTO result")]
    [InlineData("SELECT a UNION SELECT b LIMIT 1 INTO result")]
    [InlineData("(SELECT a) LIMIT 1 INTO result")]
    [InlineData("(SELECT a) UNION SELECT b LIMIT 1 INTO result")]
    [InlineData("SELECT a UNION SELECT b UNION SELECT c LIMIT 1 INTO result")]
    public void SelectInto_SingleName_IsVariableRegardlessOfQueryShape(string sql)
    {
        var select = Assert.IsType<Select>(ParseStatement(sql));
        SelectInto? target = select.SelectInto ?? (select.Body as SelectBody.SimpleSelectQuery)?.Query.SelectInto;

        var into = Assert.IsType<SelectInto.SelectIntoVariables>(target);
        Assert.Equal("result", Assert.Single(into.Variables).Name);
    }

    [Theory]
    [InlineData("SELECT a, b INTO first_result, second_result FROM source")]
    [InlineData("SELECT a, b FROM source INTO first_result, second_result")]
    public void SelectInto_MultipleNames_AreVariables(string sql)
    {
        var select = Assert.IsType<Select>(ParseStatement(sql));
        SelectInto? target = select.SelectInto ?? (select.Body as SelectBody.SimpleSelectQuery)?.Query.SelectInto;

        var into = Assert.IsType<SelectInto.SelectIntoVariables>(target);
        Assert.Equal(["first_result", "second_result"], into.Variables.Select(variable => variable.Name));
    }

    [Theory]
    [InlineData("SELECT a INTO OUTFILE 'result.txt' FROM source")]
    [InlineData("SELECT a FROM source INTO OUTFILE 'result.txt'")]
    public void SelectInto_Outfile_IsFile(string sql)
    {
        var select = Assert.IsType<Select>(ParseStatement(sql));
        SelectInto? target = select.SelectInto ?? (select.Body as SelectBody.SimpleSelectQuery)?.Query.SelectInto;

        var into = Assert.IsType<SelectInto.SelectIntoFile>(target);
        Assert.Equal("result.txt", into.FilePath);
    }

    [Theory]
    [InlineData("SELECT a INTO target_schema.result FROM source")]
    [InlineData("SELECT a FROM source INTO target_schema.result")]
    public void SelectInto_QualifiedTableTarget_Throws(string sql)
    {
        Assert.Throws<ParseException.ExpectedButFound>(() => ParseStatement(sql));
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
    [InlineData("CONVERT(col12, NATIONAL CHAR(10))", Label = "CONVERT NATIONAL CHAR")]
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

    [Fact]
    public void Convert_NonWordDataType_Throws()
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatement("CONVERT(value, *)"));
    }

    [Theory]
    [InlineData("//", "//////")]
    [InlineData("@", "@@")]
    [InlineData("$$$", "$$$$$$")]
    public void CustomDelimiter_Multiples_Gobbled(string singleDelimiter, string multipleDelimiters)
    {
        var given = $"""
        DELIMITER {singleDelimiter}

        SELECT 1{multipleDelimiters}

        DELIMITER ;
        """;

        var statements = ParseStatements(given);
        var statement = Assert.Single(statements);

        Assert.Equal("SELECT 1", statement.ToSql());
    }

    [Theory]
    [InlineData("/")]
    [InlineData("///")]
    public void CustomDelimiter_NonMultiple_Throws(string delimiter)
    {
        var given = $"""
        DELIMITER //

        SELECT 1{delimiter}

        DELIMITER ;
        """;

        Assert.Throws<ParseException.ExpectedButFound>(() => ParseStatements(given));
    }

    [Fact]
    public void Loop_Unclosed_Throws()
    {
        var given = """
        LOOP
        """;

        var ex = Assert.Throws<ParseException.ExpectedButFound>(() => ParseStatements(given));
        Assert.Contains("END LOOP", ex.Message);
    }

    [Theory]
    [InlineData("IF 1 = 1 THEN SELECT 1;")]
    [InlineData("IF 1 = 1 THEN SELECT 1; ELSEIF 1 = 0 THEN SELECT 0;")]
    public void If_Unclosed_Throws(string sql)
    {
        var ex = Assert.Throws<ParseException.ExpectedOneOfButFound>(() => ParseStatements(sql));
        Assert.Contains("{ ELSEIF | ELSE | END IF }", ex.Message);
    }

    [Theory]
    [InlineData("IF 1 = 1 THEN SELECT 1; ELSE SELECT 0;")]
    [InlineData("IF 1 = 1 THEN SELECT 1; ELSEIF 1 = 0 THEN SELECT 0; ELSE SELECT -1;")]
    public void If_WithElse_Unclosed_Throws(string sql)
    {
        var ex = Assert.Throws<ParseException.ExpectedButFound>(() => ParseStatements(sql));
        Assert.Contains("END IF", ex.Message);
    }

    [Fact]
    public void Statements_WithoutDelimiter_Throws()
    {
        var ex = Assert.Throws<ParseException.ExpectedButFound>(() => ParseStatements("SELECT 1 SELECT 1"));
        Assert.Contains("end of statement", ex.Message);
    }

    [Theory]
    [InlineData("CREATE TYPE mood AS ENUM ('happy')")]
    [InlineData("CREATE TYPE address AS (street TEXT)")]
    [InlineData("CREATE DOMAIN postcode AS TEXT")]
    public void CreateTypeAndDomain_AreNotSupported(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("INT[]")]
    [InlineData("INT ARRAY")]
    [InlineData("TEXT ARRAY[3]")]
    public void ArrayTypeDeclarations_AreNotSupported(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements($"CREATE TABLE sample (value {sql})"));
    }

    [Theory]
    [InlineData("CREATE INDEX ix ON items USING BTREE (id)")]
    [InlineData("CREATE UNIQUE INDEX ix ON items USING BTREE (id)")]
    public void PostgresIndexMethodPlacement_IsRejected(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("CREATE CLUSTERED INDEX ix ON items (id)")]
    [InlineData("CREATE NONCLUSTERED INDEX ix ON items (id)")]
    public void CreateIndex_OrganizationKeywords_TextMatch(string sql)
    {
        var (expected, actual) = GetExpectedActual(sql, expectedSql: sql, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("CREATE *")]
    [InlineData("CREATE unsupported_object")]
    public void InvalidCreateTargets_AreRejected(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("CREATE SQL SECURITY DEFINER TRIGGER trg")]
    [InlineData("CREATE SQL SECURITY INVOKER EVENT ev")]
    public void Create_SqlSecurityRejectedForTriggerAndEvent(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Fact]
    public void PostgresOperators_AreNarrowedAndDoublePipeRemainsLogicalOr()
    {
        var parser = new MyParser();
        Assert.False(parser.SupportsPostgresCastSyntax);
        Assert.False(parser.SupportsPostgresRegexOperators);
        Assert.ThrowsAny<ParseException>(() => ParseExpression("value::INT"));
        Assert.ThrowsAny<ParseException>(() => ParseExpression("value ~ 'pattern'"));
        Assert.Equal("a OR b", ParseExpression("a || b").ToSql());
    }

    [Theory]
    [InlineData("TABLE (id INT)")]
    [InlineData("SETOF INT")]
    [InlineData("trigger")]
    [InlineData("void")]
    [InlineData("record")]
    public void FunctionReturn_PostgresShapes_AreRejected(string returnClause)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements($"CREATE FUNCTION f() RETURNS {returnClause} RETURN 1"));
    }

    [Fact]
    public void ProcedureParameter_MariaDbDefaultKeyword_RoundTripsInSharedParser()
    {
        string sql = "CREATE PROCEDURE p(IN value INT DEFAULT (1 + 2)) SELECT value";
        string expectedSql = "CREATE PROCEDURE p (IN value INT DEFAULT (1 + 2)) SELECT value";
        (string? expected, string? actual) = GetExpectedActual(sql, expectedSql: expectedSql, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FunctionParameter_MariaDbDefaultKeyword_RoundTripsInSharedParser()
    {
        string sql = "CREATE FUNCTION f(IN value INT DEFAULT (1 + 2)) RETURNS INT RETURN value";
        string expectedSql = "CREATE FUNCTION f (IN value INT DEFAULT (1 + 2)) RETURNS INT RETURN value";
        (string? expected, string? actual) = GetExpectedActual(sql, expectedSql: expectedSql, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FunctionParameter_EqualsDefaultIsRejected()
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements("CREATE FUNCTION f(value INT = 1) RETURNS INT RETURN value"));
    }

    [Fact]
    public void FunctionReturn_CannotBeOmittedInSharedMySqlMariaDbParser()
    {
        var error = Assert.Throws<ParseException.ExpectedButFound>(() => ParseStatement("CREATE FUNCTION f() RETURN 1"));

        Assert.Contains("RETURNS", error.Message);
    }

    [Theory]
    [InlineData("CREATE TRIGGER audit AFTER INSERT ON items FOR EACH ROW SET NEW.id = 1")]
    [InlineData("CREATE TRIGGER audit BEFORE UPDATE ON items FOR EACH ROW FOLLOWS other SELECT 1")]
    public void Triggers_RetainMySqlMariaDbSyntax(string sql)
    {
        Assert.Equal(sql, ParseStatement(sql).ToSql());
    }

    [Theory]
    [InlineData("CREATE CONSTRAINT TRIGGER audit AFTER INSERT ON items FOR EACH ROW SELECT 1")]
    [InlineData("CREATE TRIGGER audit AFTER TRUNCATE ON items FOR EACH ROW SELECT 1")]
    [InlineData("CREATE TRIGGER audit AFTER UPDATE OF id ON items FOR EACH ROW SELECT 1")]
    [InlineData("CREATE TRIGGER audit AFTER INSERT OR UPDATE ON items FOR EACH ROW SELECT 1")]
    [InlineData("CREATE TRIGGER audit AFTER INSERT, UPDATE ON items FOR EACH ROW SELECT 1")]
    [InlineData("CREATE TRIGGER audit AFTER INSERT ON items FOR ROW SELECT 1")]
    [InlineData("CREATE TRIGGER audit AFTER INSERT ON items REFERENCING NEW TABLE new_rows FOR EACH ROW SELECT 1")]
    [InlineData("CREATE TRIGGER audit AFTER INSERT ON items FOR EACH ROW WHEN (NEW.id > 0) SELECT 1")]
    [InlineData("CREATE TRIGGER audit AFTER INSERT ON items FOR EACH ROW EXECUTE FUNCTION audit()")]
    public void PostgresTriggerSyntax_IsRejected(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("COMMENT ON TABLE items IS 'table'")]
    [InlineData("COMMENT ON COLUMN items.id IS NULL")]
    [InlineData("COMMENT ON FUNCTION f(INT) IS 'function'")]
    public void PostgresCommentSyntax_IsRejectedByMySqlAndMariaDbParser(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Fact]
    public void ColumnCollations_RetainSharedMySqlMariaDbSyntax()
    {
        const string sql = "CREATE TABLE items (label VARCHAR(20) COLLATE utf8mb4_bin NOT NULL)";

        Assert.Equal(sql, ParseStatement(sql).ToSql());
    }

    [Theory]
    [InlineData("CREATE TABLE items (label VARCHAR(20) COLLATE app.locale)")]
    [InlineData("CREATE TABLE items (label VARCHAR(20) NOT NULL COLLATE app.locale)")]
    public void PostgresColumnCollations_AreRejectedByMySqlAndMariaDbParser(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Fact]
    public void PostgresExpressionCollations_AreRejectedByMySqlAndMariaDbParser()
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements("SELECT label COLLATE app.locale FROM items"));
    }

    [Theory]
    [InlineData("SELECT ROW(1, 2)")]
    [InlineData("SELECT items.value FROM items")]
    [InlineData("looplabel /* gap */ : LOOP SELECT 1; END LOOP looplabel")]
    public void RowFunctionsAndStatementLabels_RetainSharedMySqlMariaDbSyntax(string sql)
    {
        Assert.Equal(sql.Replace(" /* gap */ ", "", StringComparison.Ordinal), ParseStatement(sql).ToSql());
    }

    [Theory]
    [InlineData("SELECT ARRAY[1, 2] + 1")]
    [InlineData("SELECT ARRAY(SELECT 1)")]
    [InlineData("SELECT arr[1:2] + 1")]
    [InlineData("SELECT (item).field")]
    [InlineData("SELECT (item).*")]
    public void PostgresArrayAndCompositeExpressions_AreRejectedBySharedMySqlMariaDbParser(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Fact]
    public void NestedLoopLabels_RestoreOuterLabelAndDoNotLeakIntoUnlabeledChildren()
    {
        const string loopSql = "outer_label: LOOP inner_label: LOOP SELECT 1; END LOOP inner_label; LOOP SELECT 2; END LOOP; SELECT 3; END LOOP outer_label";
        string sql = loopSql + "; SELECT 4";
        var state = GetState(sql);
        var statements = new MyParser().Parse(state, null);
        var outer = Assert.IsType<LabeledStatement>(statements[0]);
        var outerLoop = Assert.IsType<Loop>(outer.SubStatement);
        var inner = Assert.IsType<LabeledStatement>(outerLoop.Statements[0]);
        var innerLoop = Assert.IsType<Loop>(inner.SubStatement);
        var unlabeledLoop = Assert.IsType<Loop>(outerLoop.Statements[1]);

        Assert.Equal("outer_label", outer.Label.Name);
        Assert.Equal(outer.Label, outerLoop.EndLabel);
        Assert.Equal("inner_label", inner.Label.Name);
        Assert.Equal(inner.Label, innerLoop.EndLabel);
        Assert.Collection(innerLoop.Statements, statement => Assert.IsType<Select>(statement), statement => Assert.IsType<InertOnly>(statement));
        Assert.Null(unlabeledLoop.EndLabel);
        Assert.Collection(unlabeledLoop.Statements, statement => Assert.IsType<Select>(statement), statement => Assert.IsType<InertOnly>(statement));
        Assert.IsType<Select>(outerLoop.Statements[2]);
        Assert.IsType<Select>(statements[1]);
        Assert.Null(state.StatementLabel);
        Assert.Equal(loopSql, outer.ToSql());
    }

    [Theory]
    [InlineData(";")]
    [InlineData("$$")]
    [InlineData("//")]
    public void LoopLabels_PreserveSourceSpansWithCustomDelimiters(string delimiter)
    {
        const string loopSql = "looplabel: LOOP SELECT 1; END LOOP looplabel";
        string prefix = "DELIMITER " + delimiter + "\n";
        string sql = prefix + loopSql + delimiter;
        var statement = Assert.IsType<LabeledStatement>(ParseStatement(sql));

        Assert.Equal(prefix.Length - 1, statement.Meta.Start);
        Assert.Equal(prefix.Length + loopSql.Length, statement.Meta.End);
        Assert.Equal(sql.Length, statement.Meta.EndPlusTerminatorLength);
        Assert.Equal(statement.SubStatement.Meta.End, statement.Meta.End);
        Assert.Equal(statement.SubStatement.Meta.EndPlusTerminatorLength, statement.Meta.EndPlusTerminatorLength);
        Assert.Equal("\n" + loopSql, Core.DefinitionBuilding.SourceManager.GetText(sql, statement.Meta));
    }
}
