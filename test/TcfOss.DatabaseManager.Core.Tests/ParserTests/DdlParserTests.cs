using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Tests.ParserTests;

// ReSharper disable ClassNeverInstantiated.Global
public class DdlParserTests : ParserTestsBase<GenericLexer, Parser>
{
    [Fact]
    public void CreateFunction_RoutineParametersParseKitchenSinkUnion()
    {
        const string sql = "CREATE FUNCTION f(INT, named TEXT DEFAULT 'x', INOUT count INT = 1, IN OUT total INT, VARIADIC rest INT) RETURNS RECORD RETURN 1";
        CreateFunction function = Assert.IsType<CreateFunction>(ParseStatement(sql));

        Assert.Null(Assert.IsType<StatementRoutineParameter.General>(function.Parameters[0]).Name);
        Assert.Equal("named", Assert.IsType<StatementRoutineParameter.General>(function.Parameters[1]).Name?.Name);
        Assert.True(Assert.IsType<ParameterDefault.ExpressionDefault>(
            Assert.IsType<StatementRoutineParameter.General>(function.Parameters[1]).ParameterDefault).UseDefaultKeyword);
        Assert.Equal(RoutineParameterDirection.InOut, Assert.IsType<StatementRoutineParameter.General>(function.Parameters[2]).Direction);
        Assert.False(Assert.IsType<ParameterDefault.ExpressionDefault>(
            Assert.IsType<StatementRoutineParameter.General>(function.Parameters[2]).ParameterDefault).UseDefaultKeyword);
        Assert.Equal(RoutineParameterDirection.InOut, Assert.IsType<StatementRoutineParameter.General>(function.Parameters[3]).Direction);
        Assert.Equal(RoutineParameterDirection.Variadic, Assert.IsType<StatementRoutineParameter.General>(function.Parameters[4]).Direction);
        Assert.Equal(function, ParseStatement(function.ToSql()));
    }

    [Theory]
    [InlineData("CREATE TRIGGER mytrigger AFTER INSERT ON mytable FOR EACH STATEMENT SELECT 1")]
    [InlineData("CREATE TRIGGER mytrigger AFTER INSERT OR UPDATE OF first_column, second_column OR DELETE ON mytable FOR EACH ROW SELECT 1")]
    [InlineData("CREATE TRIGGER mytrigger AFTER TRUNCATE ON mytable SELECT 1")]
    [InlineData("CREATE TRIGGER mytrigger ON mytable AFTER INSERT OR UPDATE AS SELECT 1")]
    [InlineData("CREATE OR REPLACE CONSTRAINT TRIGGER mytrigger AFTER INSERT ON mytable FOR EACH ROW EXECUTE FUNCTION check_row()")]
    [InlineData("CREATE CONSTRAINT TRIGGER mytrigger AFTER UPDATE ON mytable FROM other_table DEFERRABLE INITIALLY DEFERRED FOR ROW WHEN (NEW.id > 0) EXECUTE FUNCTION check_row('key', 1, named)")]
    [InlineData("CREATE CONSTRAINT TRIGGER mytrigger AFTER DELETE ON mytable NOT DEFERRABLE INITIALLY IMMEDIATE FOR EACH ROW EXECUTE PROCEDURE check_row()")]
    [InlineData("CREATE TRIGGER mytrigger AFTER UPDATE ON mytable REFERENCING OLD TABLE AS old_rows NEW TABLE new_rows FOR STATEMENT EXECUTE FUNCTION check_rows()")]
    [InlineData("CREATE TRIGGER mytrigger AFTER INSERT ON mytable WHEN (NEW.id > 0) EXECUTE FUNCTION check_row()")]
    [InlineData("CREATE TRIGGER mytrigger AFTER INSERT ON mytable FOR EACH ROW SELECT 1", Label = "CREATE TRIGGER basic")]
    [InlineData("CREATE OR REPLACE TRIGGER mytrigger AFTER INSERT ON mytable FOR EACH ROW SELECT 1", Label = "CREATE OR REPLACE TRIGGER")]
    [InlineData("CREATE OR ALTER TRIGGER mytrigger AFTER INSERT ON mytable FOR EACH ROW SELECT 1", Label = "CREATE OR ALTER TRIGGER")]
    [InlineData("CREATE TRIGGER IF NOT EXISTS mytrigger AFTER INSERT ON mytable FOR EACH ROW SELECT 1", Label = "CREATE TRIGGER IF NOT EXISTS")]
    [InlineData("CREATE TRIGGER mytrigger AFTER UPDATE ON mytable FOR EACH ROW SELECT 1", Label = "CREATE TRIGGER AFTER UPDATE")]
    [InlineData("CREATE TRIGGER mytrigger AFTER DELETE ON mytable FOR EACH ROW SELECT 1", Label = "CREATE TRIGGER AFTER DELETE")]
    [InlineData("CREATE TRIGGER mytrigger BEFORE INSERT ON mytable FOR EACH ROW SELECT 1", Label = "CREATE TRIGGER BEFORE INSERT")]
    [InlineData("CREATE TRIGGER mytrigger BEFORE UPDATE ON mytable FOR EACH ROW SELECT 1", Label = "CREATE TRIGGER BEFORE UPDATE")]
    [InlineData("CREATE TRIGGER mytrigger BEFORE DELETE ON mytable FOR EACH ROW SELECT 1", Label = "CREATE TRIGGER BEFORE DELETE")]
    [InlineData("CREATE TRIGGER mytrigger BEFORE DELETE ON mytable FOR EACH ROW BEGIN SELECT 1; SELECT 2; END", Label = "CREATE TRIGGER block body")]
    [InlineData("CREATE DEFINER = 'user'@'host' TRIGGER mytrigger BEFORE DELETE ON mytable FOR EACH ROW SELECT 1", Label = "CREATE TRIGGER DEFINER user host")]
    [InlineData("CREATE DEFINER = CURRENT_USER TRIGGER mytrigger BEFORE DELETE ON mytable FOR EACH ROW SELECT 1", Label = "CREATE TRIGGER DEFINER CURRENT_USER")]
    [InlineData("CREATE TRIGGER mytrigger AFTER INSERT ON mytable FOR EACH ROW FOLLOWS othertrigger SELECT 1", Label = "CREATE TRIGGER FOLLOWS")]
    [InlineData("CREATE TRIGGER mytrigger AFTER INSERT ON mytable FOR EACH ROW PRECEDES othertrigger SELECT 1", Label = "CREATE TRIGGER PRECEDES")]
    public static void CreateTrigger_TextMatch(string sql)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.Parse, sql);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("CREATE FUNCTION myfunc () RETURNS INT RETURN 1", Label = "CREATE FUNCTION basic")]
    [InlineData("CREATE FUNCTION myfunc (arg INT) RETURNS INT RETURN 1", Label = "CREATE FUNCTION one param")]
    [InlineData("CREATE FUNCTION myfunc (arg1 INT, arg2 VARCHAR(5), arg3 INT) RETURNS INT RETURN 1", Label = "CREATE FUNCTION many params")]
    [InlineData("CREATE FUNCTION myfunc (IN arg1 INT, OUT arg2 VARCHAR(5), INOUT arg3 INT) RETURNS INT RETURN 1", Label = "CREATE FUNCTION param modes")]
    [InlineData("CREATE OR REPLACE FUNCTION myfunc () RETURNS INT RETURN 1", Label = "CREATE OR REPLACE FUNCTION")]
    [InlineData("CREATE OR ALTER FUNCTION myfunc () RETURNS INT RETURN 1", Label = "CREATE OR ALTER FUNCTION")]
    [InlineData("CREATE FUNCTION IF NOT EXISTS myfunc () RETURNS INT RETURN 1", Label = "CREATE FUNCTION IF NOT EXISTS")]
    [InlineData("CREATE AGGREGATE FUNCTION myfunc () RETURNS INT RETURN 1", Label = "CREATE AGGREGATE FUNCTION")]
    [InlineData("CREATE FUNCTION myfunc () RETURNS INT BEGIN SELECT 1; RETURN 1; END", Label = "CREATE FUNCTION block body")]
    [InlineData("CREATE FUNCTION myfunc () RETURNS INT MODIFIES SQL DATA RETURN 1", Label = "CREATE FUNCTION MODIFIES SQL DATA")]
    [InlineData("CREATE FUNCTION myfunc () RETURNS INT LANGUAGE SQL CONTAINS SQL RETURN 1", Label = "CREATE FUNCTION LANGUAGE SQL")]
    [InlineData("CREATE FUNCTION myfunc () RETURNS INT DETERMINISTIC READS SQL DATA RETURN 1", Label = "CREATE FUNCTION DETERMINISTIC READS")]
    [InlineData("CREATE FUNCTION myfunc () RETURNS INT NOT DETERMINISTIC NO SQL RETURN 1", Label = "CREATE FUNCTION NOT DETERMINISTIC")]
    [InlineData("CREATE FUNCTION myfunc () RETURNS INT COMMENT 'this is my func' RETURN 1", Label = "CREATE FUNCTION COMMENT")]
    [InlineData("CREATE DEFINER = 'user'@'localhost' FUNCTION myfunc () RETURNS INT SQL SECURITY INVOKER RETURN 1", Label = "CREATE FUNCTION DEFINER user host")]
    public static void CreateFunction_TextMatch(string sql)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.Parse, sql);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("CREATE PROCEDURE myproc () SELECT a, b", Label = "CREATE PROCEDURE basic")]
    [InlineData("CREATE PROCEDURE myproc (arg INT) SELECT a, b", Label = "CREATE PROCEDURE one param")]
    [InlineData("CREATE PROCEDURE myproc (arg1 INT, arg2 VARCHAR(5), arg3 INT) SELECT a, b", Label = "CREATE PROCEDURE many params")]
    [InlineData("CREATE PROCEDURE myproc (IN arg1 INT, OUT arg2 VARCHAR(5), INOUT arg3 INT) SELECT a, b", Label = "CREATE PROCEDURE param modes")]
    [InlineData("CREATE OR REPLACE PROCEDURE myproc () SELECT a, b", Label = "CREATE OR REPLACE PROCEDURE")]
    [InlineData("CREATE OR ALTER PROCEDURE myproc () SELECT a, b", Label = "CREATE OR ALTER PROCEDURE")]
    [InlineData("CREATE PROCEDURE IF NOT EXISTS myproc () SELECT a, b", Label = "CREATE PROCEDURE IF NOT EXISTS")]
    [InlineData("CREATE PROCEDURE myproc () BEGIN SELECT 1; SELECT a, b; END", Label = "CREATE PROCEDURE block body")]
    [InlineData("CREATE PROCEDURE myproc () MODIFIES SQL DATA SELECT a, b", Label = "CREATE PROCEDURE MODIFIES SQL DATA")]
    [InlineData("CREATE PROCEDURE myproc () LANGUAGE SQL CONTAINS SQL SELECT a, b", Label = "CREATE PROCEDURE LANGUAGE SQL")]
    [InlineData("CREATE PROCEDURE myproc () DETERMINISTIC READS SQL DATA SELECT a, b", Label = "CREATE PROCEDURE DETERMINISTIC READS")]
    [InlineData("CREATE PROCEDURE myproc () NOT DETERMINISTIC NO SQL SELECT a, b", Label = "CREATE PROCEDURE NOT DETERMINISTIC")]
    [InlineData("CREATE PROCEDURE myproc () COMMENT 'this is my func' SELECT a, b", Label = "CREATE PROCEDURE COMMENT")]
    [InlineData("CREATE PROCEDURE myproc () COMMENT 'this is my func' LANGUAGE SQL SELECT a, b", Label = "CREATE PROCEDURE COMMENT LANGUAGE SQL")]
    [InlineData("CREATE PROCEDURE myproc () LANGUAGE SQL DETERMINISTIC SELECT a, b", Label = "CREATE PROCEDURE LANGUAGE SQL DETERMINISTIC")]
    [InlineData("CREATE PROCEDURE myproc () DETERMINISTIC SQL SECURITY DEFINER SELECT a, b", Label = "CREATE PROCEDURE DETERMINISTIC SQL SECURITY DEFINER")]
    [InlineData("CREATE DEFINER = 'user'@'localhost' PROCEDURE myproc () SQL SECURITY INVOKER SELECT a, b", Label = "CREATE PROCEDURE DEFINER user host")]
    public static void CreateProcedure_TextMatch(string sql)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.Parse, sql);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("TABLE (id INT, label TEXT)")]
    [InlineData("SETOF INT ARRAY")]
    [InlineData("SETOF app.items")]
    [InlineData("SETOF record")]
    [InlineData("trigger")]
    [InlineData("void")]
    [InlineData("record")]
    public void FunctionReturnShapes_RoundTrip(string returnClause)
    {
        var function = Assert.IsType<CreateFunction>(ParseStatement($"CREATE FUNCTION f() RETURNS {returnClause} RETURN 1"));

        Assert.Equal(function, ParseStatement(function.ToSql()));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" VARYING")]
    [InlineData(" NULL")]
    [InlineData(" NOT NULL")]
    [InlineData(" = 1 OUTPUT")]
    [InlineData(" READONLY")]
    [InlineData(" VARYING NULL = 1 OUTPUT READONLY")]
    [InlineData(" NOT NULL = 1 OUT")]
    public void RoutineParameterOptions_PreserveOptionsBeforeAndAfterDefault(string suffix)
    {
        var function = Assert.IsType<CreateFunction>(ParseStatement($"CREATE FUNCTION f(@value INT{suffix}) RETURNS INT RETURN 1"));
        var parameter = Assert.IsType<StatementRoutineParameter.General>(Assert.Single(function.Parameters));

        Assert.Equal($"@value INT{suffix}", parameter.ToSql());
        Assert.Equal(function, ParseStatement(function.ToSql()));
        Assert.Equal(suffix.Length > 0, parameter.Options != null);
    }

    [Theory]
    [InlineData("CREATE VIEW myview AS SELECT a, b", Label = "CREATE VIEW basic")]
    [InlineData("CREATE VIEW myview AS VALUES (1)", Label = "CREATE VIEW values query")]
    [InlineData("CREATE VIEW myview AS (SELECT 1)", Label = "CREATE VIEW parenthesized query")]
    [InlineData("CREATE TEMP VIEW myview AS SELECT a, b", Label = "CREATE VIEW temporary abbreviation")]
    [InlineData("CREATE TEMPORARY VIEW myview AS SELECT a, b", Label = "CREATE VIEW temporary")]
    [InlineData("CREATE RECURSIVE VIEW myview (id) AS SELECT 1 UNION ALL SELECT id FROM myview", Label = "CREATE VIEW recursive")]
    [InlineData("CREATE OR REPLACE TEMP RECURSIVE VIEW myview (id) AS SELECT 1", Label = "CREATE VIEW combined modifiers")]
    [InlineData("CREATE VIEW myview AS SELECT a, b WITH CHECK OPTION", Label = "CREATE VIEW implicit check mode")]
    [InlineData("CREATE VIEW myview WITH (security_barrier = TRUE, security_invoker) AS SELECT a, b", Label = "CREATE VIEW PostgreSQL options")]
    [InlineData("CREATE OR REPLACE VIEW myview AS SELECT a, b", Label = "CREATE OR REPLACE VIEW")]
    [InlineData("CREATE OR ALTER VIEW myview AS SELECT a, b", Label = "CREATE OR ALTER VIEW")]
    [InlineData("CREATE VIEW IF NOT EXISTS myview AS SELECT a, b", Label = "CREATE VIEW IF NOT EXISTS")]
    [InlineData("CREATE ALGORITHM = UNDEFINED DEFINER = CURRENT_USER VIEW myview AS SELECT a, b", Label = "CREATE ALGORITHM UNDEFINED DEFINER CURRENT_USER VIEW")]
    [InlineData("CREATE ALGORITHM = MERGE DEFINER = CURRENT_ROLE SQL SECURITY DEFINER VIEW myview AS SELECT a, b", Label = "CREATE ALGORITHM MERGE DEFINER CURRENT_ROLE VIEW")]
    [InlineData("CREATE ALGORITHM = TEMPTABLE DEFINER = 'user'@'localhost' SQL SECURITY INVOKER VIEW myview AS SELECT a, b", Label = "CREATE ALGORITHM TEMPTABLE INVOKER VIEW")]
    [InlineData("CREATE ALGORITHM = TEMPTABLE DEFINER = 'user'@'localhost' SQL SECURITY INVOKER VIEW myview AS SELECT a, b WITH LOCAL CHECK OPTION", Label = "CREATE ALGORITHM TEMPTABLE WITH LOCAL CHECK OPTION")]
    [InlineData("CREATE VIEW myview (a, b) AS WITH cte AS (SELECT a, b FROM othertable) SELECT cte.b, mt.b, mt.d FROM mytable AS mt INNER JOIN cte ON mt.a = cte.a WITH CASCADED CHECK OPTION", Label = "CREATE VIEW complex WITH CASCADED CHECK OPTION")]
    public static void CreateView_TextMatch(string sql)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.Parse, sql);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CreateView_IfNotExistsPreservesKitchenSinkGrammar()
    {
        var statement = Assert.IsType<CreateView>(ParseStatement("CREATE VIEW IF NOT EXISTS myview AS SELECT 1"));

        Assert.True(statement.IfNotExists);
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
    }

    [Theory]
    [InlineData("INSERT INTO items VALUES (1)")]
    [InlineData("UPDATE items SET id = 1")]
    [InlineData("DELETE FROM items")]
    [InlineData("WITH seed AS (SELECT 1) DELETE FROM items")]
    [InlineData("CALL app.run()")]
    public void CreateView_InvalidQueryBody_ThrowsParseException(string body)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatement($"CREATE VIEW v AS {body}"));
    }

    [Theory]
    [InlineData("CREATE EVENT myevent ON SCHEDULE AT CURRENT_TIMESTAMP() DO INSERT INTO mytable VALUES (NOW())", Label = "CREATE EVENT AT CURRENT_TIMESTAMP")]
    [InlineData("CREATE EVENT myevent ON SCHEDULE AT '2025-07-01 08:52:00' DO INSERT INTO mytable VALUES (NOW())", Label = "CREATE EVENT AT LITERAL TIMESTAMP")]
    [InlineData("CREATE EVENT myevent ON SCHEDULE AT CURRENT_TIMESTAMP() + INTERVAL 1 DAY DO INSERT INTO mytable VALUES (NOW())", Label = "CREATE EVENT AT INTERVAL 1 DAY")]
    [InlineData("CREATE EVENT myevent ON SCHEDULE AT CURRENT_TIMESTAMP() + INTERVAL 1 DAY + INTERVAL 5 MINUTE DO INSERT INTO mytable VALUES (NOW())", Label = "CREATE EVENT AT INTERVAL 1 DAY + 5 MIN")]
    [InlineData("CREATE EVENT myevent ON SCHEDULE EVERY 1 DAY DO INSERT INTO mytable VALUES (NOW())", Label = "CREATE EVENT EVERY 1 DAY")]
    [InlineData("CREATE EVENT myevent ON SCHEDULE EVERY 1 DAY STARTS CURRENT_TIMESTAMP() + INTERVAL 1 DAY DO INSERT INTO mytable VALUES (NOW())", Label = "CREATE EVENT EVERY 1 DAY STARTS")]
    [InlineData("CREATE EVENT myevent ON SCHEDULE EVERY 1 DAY ENDS CURRENT_TIMESTAMP() + INTERVAL 1 DAY DO INSERT INTO mytable VALUES (NOW())", Label = "CREATE EVENT EVERY 1 DAY ENDS")]
    [InlineData("CREATE EVENT myevent ON SCHEDULE EVERY 1 DAY STARTS CURRENT_TIMESTAMP() + INTERVAL 1 MICROSECOND ENDS CURRENT_TIMESTAMP() + INTERVAL 1 WEEK DO INSERT INTO mytable VALUES (NOW())", Label = "CREATE EVENT complex schedule")]
    [InlineData("CREATE DEFINER = CURRENT_USER EVENT myevent ON SCHEDULE EVERY 1 DAY DO INSERT INTO mytable VALUES (NOW())", Label = "CREATE EVENT DEFINER CURRENT_USER")]
    [InlineData("CREATE OR REPLACE EVENT myevent ON SCHEDULE EVERY 1 DAY DO INSERT INTO mytable VALUES (NOW())", Label = "CREATE OR REPLACE EVENT")]
    [InlineData("CREATE EVENT IF NOT EXISTS myevent ON SCHEDULE EVERY 1 DAY DO INSERT INTO mytable VALUES (NOW())", Label = "CREATE EVENT IF NOT EXISTS")]
    [InlineData("CREATE EVENT myevent ON SCHEDULE EVERY 1 SECOND ON COMPLETION PRESERVE DO INSERT INTO mytable VALUES (NOW())", Label = "CREATE EVENT EVERY 1 SECOND PRESERVE")]
    [InlineData("CREATE EVENT myevent ON SCHEDULE EVERY 1 MINUTE ON COMPLETION NOT PRESERVE DO INSERT INTO mytable VALUES (NOW())", Label = "CREATE EVENT EVERY 1 MINUTE NOT PRESERVE")]
    [InlineData("CREATE EVENT myevent ON SCHEDULE EVERY 1 HOUR ENABLE DO INSERT INTO mytable VALUES (NOW())", Label = "CREATE EVENT EVERY 1 HOUR ENABLE")]
    [InlineData("CREATE EVENT myevent ON SCHEDULE EVERY 1 WEEK DISABLE DO INSERT INTO mytable VALUES (NOW())", Label = "CREATE EVENT EVERY 1 WEEK DISABLE")]
    [InlineData("CREATE EVENT myevent ON SCHEDULE EVERY 1 MONTH DISABLE ON REPLICA DO INSERT INTO mytable VALUES (NOW())", Label = "CREATE EVENT EVERY 1 MONTH DISABLE ON REPLICA")]
    [InlineData("CREATE EVENT myevent ON SCHEDULE EVERY 1 QUARTER DISABLE ON SLAVE DO INSERT INTO mytable VALUES (NOW())", Label = "CREATE EVENT EVERY 1 QUARTER DISABLE ON SLAVE")]
    [InlineData("CREATE EVENT myevent ON SCHEDULE EVERY 1 YEAR COMMENT 'this is my proc' DO INSERT INTO mytable VALUES (NOW())", Label = "CREATE EVENT EVERY 1 YEAR COMMENT")]
    public static void CreateEvent_TextMatch(string sql)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.Parse, sql);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("DROP SCHEMA myschema")]
    [InlineData("DROP DATABASE mydatabase")]
    [InlineData("DROP TABLE mytable")]
    [InlineData("DROP TABLE IF EXISTS mytable")]
    [InlineData("DROP TEMPORARY TABLE mytable")]
    [InlineData("DROP TEMPORARY TABLE IF EXISTS mytable")]
    [InlineData("DROP VIEW myview")]
    [InlineData("DROP VIEW IF EXISTS myview")]
    [InlineData("DROP TRIGGER mytrigger")]
    [InlineData("DROP TRIGGER IF EXISTS mytrigger")]
    [InlineData("DROP FUNCTION myfunc")]
    [InlineData("DROP FUNCTION IF EXISTS myfunc")]
    [InlineData("DROP PROCEDURE myproc")]
    [InlineData("DROP PROCEDURE IF EXISTS myproc")]
    [InlineData("DROP EVENT myevent")]
    [InlineData("DROP EVENT IF EXISTS myevent")]
    [InlineData("DROP TYPE IF EXISTS app.mood, app.address CASCADE")]
    [InlineData("DROP DOMAIN app.label RESTRICT")]
    [InlineData("DROP TABLE app.items CASCADE")]
    [InlineData("DROP VIEW visible RESTRICT")]
    [InlineData("DROP INDEX items_idx, other_idx CASCADE")]
    [InlineData("DROP SCHEMA app, archive RESTRICT")]
    public static void DropObject_TextMatch(string sql)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.Parse, sql);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("TRUNCATE TABLE mytable")]
    [InlineData("TRUNCATE TABLE mySchema.mytable")]
    [InlineData("TRUNCATE mytable")]
    public static void TruncateTable_TextMatch(string sql)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.Parse, sql);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("CREATE INDEX my_index ON mytable (my_id)", Label = "CREATE INDEX basic")]
    [InlineData("CREATE INDEX my_index ON mySchema.mytable (my_id)", Label = "CREATE INDEX qualified table")]
    [InlineData("CREATE INDEX my_index ON mytable (my_id, my_val)", Label = "CREATE INDEX multi-column")]
    [InlineData("CREATE INDEX my_index ON mytable (my_id, my_val(5))", Label = "CREATE INDEX prefix length")]
    [InlineData("CREATE INDEX my_index ON mytable (my_id ASC, my_val DESC)", Label = "CREATE INDEX with directions")]
    [InlineData("CREATE INDEX my_index ON mytable ((my_id + 1))", Label = "CREATE INDEX expression")]
    [InlineData("CREATE INDEX my_index ON mytable (my_id) COMMENT 'My index'", Label = "CREATE INDEX with comment")]
    [InlineData("CREATE INDEX my_index ON mytable (my_id) COMMENT = 'My index'", Label = "CREATE INDEX with comment equals")]
    [InlineData("CREATE INDEX IF NOT EXISTS my_index ON mytable (my_id)", Label = "CREATE INDEX IF NOT EXISTS")]
    [InlineData("CREATE OR REPLACE INDEX my_index ON mytable (my_id)", Label = "CREATE OR REPLACE INDEX")]
    [InlineData("CREATE UNIQUE INDEX my_index ON mytable (my_id)", Label = "CREATE UNIQUE INDEX")]
    [InlineData("CREATE FULLTEXT INDEX my_index ON mytable (my_val)", Label = "CREATE FULLTEXT INDEX")]
    [InlineData("CREATE SPATIAL INDEX my_index ON mytable (my_val)", Label = "CREATE SPATIAL INDEX")]
    [InlineData("CREATE INDEX my_index USING BTREE ON mytable (my_id)", Label = "CREATE INDEX USING BTREE before ON")]
    [InlineData("CREATE INDEX my_index USING HASH ON mytable (my_id)", Label = "CREATE INDEX USING HASH before ON")]
    [InlineData("CREATE UNIQUE INDEX my_index USING BTREE ON mytable (my_id) COMMENT 'Unique'", Label = "CREATE UNIQUE INDEX full")]
    [InlineData("CREATE NONCLUSTERED INDEX my_index ON mytable (my_id)", Label = "CREATE NONCLUSTERED INDEX")]
    [InlineData("CREATE CLUSTERED INDEX my_index ON mytable (my_id)", Label = "CREATE CLUSTERED INDEX")]
    [InlineData("CREATE UNIQUE NONCLUSTERED INDEX my_index ON mytable (my_id)", Label = "CREATE UNIQUE NONCLUSTERED INDEX")]
    [InlineData("CREATE UNIQUE CLUSTERED INDEX my_index ON mytable (my_id)", Label = "CREATE UNIQUE CLUSTERED INDEX")]
    [InlineData("CREATE INDEX my_index ON mytable (my_id) INCLUDE (my_val1, my_val2)", Label = "CREATE INDEX with included columns")]
    [InlineData("CREATE UNIQUE INDEX my_index ON mytable (my_id) INCLUDE (my_val1, my_val2)", Label = "CREATE UNIQUE INDEX with included columns")]
    [InlineData("CREATE INDEX my_index ON mytable (my_id) WHERE status = 1 WITH (PAD_INDEX = ON, FILLFACTOR = 80, IGNORE_DUP_KEY = ON, STATISTICS_NORECOMPUTE = ON, STATISTICS_INCREMENTAL = ON, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = ON) ON myfilegroup", Label = "CREATE INDEX with WHERE, WITH options, ON filegroup")]
    [InlineData("CREATE UNIQUE INDEX my_index ON mytable (my_id) WHERE active = 1 WITH (PAD_INDEX = OFF, FILLFACTOR = 70) ON partition_scheme (partition_column)", Label = "CREATE UNIQUE INDEX with WHERE, WITH options, ON partition scheme")]
    [InlineData("CREATE CLUSTERED INDEX my_index ON mytable (my_id) WITH (ALLOW_ROW_LOCKS = OFF, ALLOW_PAGE_LOCKS = OFF) ON myfilegroup", Label = "CREATE CLUSTERED INDEX with WITH options, ON filegroup")]
    [InlineData("CREATE NONCLUSTERED INDEX my_index ON mytable (my_id) INCLUDE (my_val) WHERE my_id > 100 WITH (STATISTICS_INCREMENTAL = ON) ON partition_scheme (col)", Label = "CREATE NONCLUSTERED INDEX with INCLUDE, WHERE, WITH, ON")]
    public static void CreateIndex_TextMatch(string sql)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.Parse, sql);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("CREATE INDEX my_index ON mytable (my_id) USING BTREE", "CREATE INDEX my_index USING BTREE ON mytable (my_id)")]
    [InlineData("CREATE UNIQUE INDEX my_index ON mytable (my_id) USING HASH", "CREATE UNIQUE INDEX my_index USING HASH ON mytable (my_id)")]
    public static void CreateIndex_IndexMethod_PrintedBeforeOn(string sql, string expected)
    {
        var parser = new Parser();
        var (_, actual) = GetExpectedActual(parser.Parse, sql, expected);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("CREATE INDEX my_index USING BTREE ON mytable (my_id) USING HASH")]
    [InlineData("CREATE INDEX my_index USING BTREE ON mytable USING HASH (my_id)")]
    [InlineData("CREATE UNIQUE INDEX my_index USING HASH ON mytable USING BTREE (my_id)")]
    public static void CreateIndex_DuplicateIndexMethod_Throws(string sql)
    {
        var parser = new Parser();

        Assert.Throws<ParseException.Duplicate>(() => parser.Parse(GetTokens(sql)));
    }

    [Theory]
    [InlineData("DROP TABLE mytable", false, false)]
    [InlineData("DROP TABLE IF EXISTS mytable", false, true)]
    [InlineData("DROP TEMPORARY TABLE mytable", true, false)]
    [InlineData("DROP TEMPORARY TABLE IF EXISTS mytable", true, true)]
    public static void DropTable_TemporaryAndIfExists_AreParsed(string sql, bool expectedTemporary, bool expectedIfExists)
    {
        var stmt = Assert.IsType<DropObject>(ParseStatement(sql));

        Assert.Equal(DroppableObject.Table, stmt.ObjectType);
        Assert.Equal(expectedTemporary, stmt.Temporary);
        Assert.Equal(expectedIfExists, stmt.IfExists);
    }

    [Theory]
    [InlineData("DROP INDEX my_index ON mytable", "my_index", "mytable")]
    [InlineData("DROP INDEX IF EXISTS my_index ON mytable", "my_index", "mytable")]
    [InlineData("DROP INDEX `my_index` ON `my_schema`.`my_table`", "`my_index`", "`my_schema`.`my_table`")]
    public static void DropIndex_ParsesObjectAndOnTarget(string sql, string expectedIndexName, string expectedTableName)
    {
        var stmt = Assert.IsType<DropObject>(ParseStatement(sql));

        Assert.Equal(DroppableObject.Index, stmt.ObjectType);
        var item = Assert.Single(stmt.Names);
        Assert.Equal(expectedIndexName, item.ToString());
        Assert.NotNull(stmt.OnObject);
        Assert.Equal(expectedTableName, stmt.OnObject!.ToString());
        Assert.Equal(sql.Contains("IF EXISTS"), stmt.IfExists);
    }

    [Theory]
    [InlineData("DROP TABLE mytable1, mytable2, mytable3", "TABLE", 3)]
    [InlineData("DROP TABLE IF EXISTS mytable1, mytable2", "TABLE", 2)]
    [InlineData("DROP VIEW myview1, myview2", "VIEW", 2)]
    [InlineData("DROP VIEW IF EXISTS myview1, myview2, myview3", "VIEW", 3)]
    [InlineData("DROP TYPE app.mood, app.address CASCADE", "TYPE", 2)]
    [InlineData("DROP DOMAIN IF EXISTS app.label, other_label RESTRICT", "DOMAIN", 2)]
    [InlineData("DROP INDEX items_idx, other_idx", "INDEX", 2)]
    [InlineData("DROP SCHEMA app, archive", "SCHEMA", 2)]
    public static void DropObject_MultipleNames_AreParsed(string sql, string expectedTypeName, int expectedCount)
    {
        var stmt = Assert.IsType<DropObject>(ParseStatement(sql));

        Assert.Equal(expectedTypeName, stmt.ObjectType.ToString());
        Assert.Equal(expectedCount, stmt.Names.Count);
        Assert.Equal(sql.Contains("IF EXISTS"), stmt.IfExists);
        Assert.Null(stmt.OnObject);
    }


    [Theory]
    [InlineData("CREATE TRIGGER mytrigger UPDATE ON mytable FOR EACH ROW SELECT 1", "{ BEFORE | AFTER | INSTEAD OF | FOR }")]
    [InlineData("CREATE TRIGGER mytrigger AFTER ON mytable FOR EACH ROW SELECT 1", "{ INSERT | UPDATE | DELETE | TRUNCATE }")]
    [InlineData("CREATE ALGORITHM = INVALID VIEW myview AS SELECT 1 FROM mytable", "{ UNDEFINED | MERGE | TEMPTABLE }")]
    [InlineData("CREATE EVENT myevent ON SCHEDULE MISSING", "{ AT | EVERY }")]
    [InlineData("CREATE PROCEDURE myproc () SQL SECURITY MISSING SELECT 1", "{ DEFINER | INVOKER }")]
    [InlineData("CREATE FUNCTION myfunc () RETURNS INT SQL SECURITY MISSING RETURN 1", "{ DEFINER | INVOKER }")]
    public static void MissingAttributes_OneOf_Throws(string sql, string missingParameters)
    {
        var parser = new Parser();
        var tokens = GetTokens(sql);

        var exc = Assert.Throws<ParseException.ExpectedOneOfButFound>(() => parser.Parse(tokens));
        Assert.StartsWith("Expected one of " + missingParameters, exc.Message);
    }

    [Theory]
    [InlineData("CREATE VIEW myview AS SELECT 1 FROM mytable WITH MISSING CHECK OPTION", "CHECK")]
    [InlineData("CREATE PROCEDURE myproc () LANGUAGE MISSING SELECT 1", "SQL")]
    public static void MissingAttributes_Single_Throws(string sql, string missingParameter)
    {
        var parser = new Parser();
        var tokens = GetTokens(sql);

        var exc = Assert.Throws<ParseException.ExpectedButFound>(() => parser.Parse(tokens));
        Assert.StartsWith($"Expected {missingParameter}. Found ", exc.Message);
    }

    [Theory]
    [InlineData("CREATE TRIGGER mytrigger AFTER")]
    [InlineData("CREATE TRIGGER mytrigger AFTER +")]
    [InlineData("CREATE TRIGGER mytrigger AFTER UNKNOWN ON mytable SELECT 1")]
    [InlineData("CREATE TRIGGER mytrigger AFTER INSERT ON mytable FOR EACH")]
    [InlineData("CREATE TRIGGER mytrigger AFTER INSERT ON mytable FOR EACH +")]
    [InlineData("CREATE TRIGGER mytrigger AFTER INSERT ON mytable FOR EACH UNKNOWN SELECT 1")]
    [InlineData("CREATE PROCEDURE myproc () WITH UNKNOWN SELECT 1")]
    [InlineData("CREATE PROCEDURE myproc () WITH ENCRYPTION, UNKNOWN SELECT 1")]
    [InlineData("CREATE EVENT myevent ON SCHEDULE EVERY 1 UNKNOWN DO SELECT 1")]
    [InlineData("DROP UNKNOWN myobject")]
    public void InvalidStatement_ThrowsParseException(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => new Parser().Parse(GetTokens(sql)));
    }

    [Theory]
    [InlineData("CREATE VIEW myview WITH SCHEMABINDING, SCHEMABINDING AS SELECT 1")]
    [InlineData("CREATE PROCEDURE myproc () WITH ENCRYPTION, ENCRYPTION SELECT 1")]
    [InlineData("CREATE PROCEDURE myproc () WITH RECOMPILE, RECOMPILE SELECT 1")]
    [InlineData("CREATE PROCEDURE myproc () WITH EXECUTE AS CALLER, EXECUTE AS OWNER SELECT 1")]
    public void DuplicateOptions_ThrowsDuplicate(string sql)
    {
        Assert.Throws<ParseException.Duplicate>(() => new Parser().Parse(GetTokens(sql)));
    }

    [Fact]
    public void CreateProcedure_WrongKeyword_Throws()
    {
        var parser = new Parser();

        var exception = Assert.Throws<ParseException.ExpectedOneOfButFound>(() =>
            parser.DdlParser.ParseCreateProcedure(new ParserState(GetTokens("FUNCTION myfunc () RETURNS INT RETURN 1"))));

        Assert.Contains("PROCEDURE", exception.Message);
        Assert.Contains("PROC", exception.Message);
    }

    [Theory]
    [InlineData("CREATE TABLE sample (value app.mood)")]
    [InlineData("CREATE FUNCTION echo (value app.mood) RETURNS app.mood RETURN value")]
    [InlineData("SELECT CAST('happy' AS app.mood)")]
    public void CoreStatements_AcceptNamedTypeReferences(string sql)
    {
        Assert.Equal(sql, ParseStatement(sql).ToSql());
    }

    [Theory]
    [InlineData("CREATE TYPE mood AS ENUM ('sad', 'happy')")]
    [InlineData("CREATE TYPE app.mood AS ENUM ()")]
    [InlineData("CREATE TYPE mood AS ENUM ('', 'it''s fine')")]
    [InlineData("CREATE TYPE mood AS ENUM ('happy', 'Happy')")]
    [InlineData("CREATE TYPE address AS (street TEXT, postcode app.postal_code)")]
    [InlineData("CREATE TYPE empty AS ()")]
    [InlineData("CREATE TYPE address AS (street TEXT COLLATE app.collation)")]
    [InlineData("CREATE TYPE address AS (Street TEXT, street TEXT)")]
    [InlineData("CREATE TYPE address AS (\"MyAttribute\" TEXT, myattribute TEXT)")]
    public void CreateType_CoreParserRoundTrips(string sql)
    {
        Assert.Equal(sql, ParseStatement(sql).ToSql());
    }

    [Fact]
    public void CreateType_EnumeratedPreservesLabelsAndSource()
    {
        var statement = Assert.IsType<CreateType.Enumerated>(Assert.Single(ParseStatements("CREATE TYPE app.mood AS ENUM ('sad', 'happy', '')")));

        Assert.Equal("app.mood", statement.Name.ToSql());
        Assert.Equal(new[] { "sad", "happy", "" }, statement.Labels);
        Assert.NotNull(statement.Name.Source);
        Assert.NotNull(statement.Meta.Start);
        Assert.NotNull(statement.Meta.End);
    }

    [Theory]
    [InlineData("'happy'")]
    [InlineData("''")]
    [InlineData("'it''s fine'")]
    public void CreateType_EnumeratedRejectsDuplicateLabelsAtSecondLiteral(string label)
    {
        string sql = $"CREATE TYPE mood AS ENUM ({label},\n  {label})";

        var exception = Assert.Throws<ParseException.Duplicate>(() => ParseStatements(sql));

        Assert.Contains("enum label", exception.Message);
        var formatted = new ParseException.WithText(exception, "types.sql", sql);
        Assert.Contains("In file 'types.sql' at line 2, column 3", formatted.Message);
        Assert.Contains("\n  ^", formatted.Message);
    }

    [Theory]
    [InlineData("field", "field")]
    [InlineData("field", "\"field\"")]
    [InlineData("\"Field\"", "\"Field\"")]
    public void CreateType_CompositeRejectsDuplicateAttributeAtSecondName(string first, string second)
    {
        string sql = $"CREATE TYPE details AS ({first} TEXT,\n  {second} INT)";

        var exception = Assert.Throws<ParseException.Duplicate>(() => ParseStatements(sql));

        Assert.Contains("composite attribute", exception.Message);
        var formatted = new ParseException.WithText(exception, "types.sql", sql);
        Assert.Contains("In file 'types.sql' at line 2, column 3", formatted.Message);
        Assert.Contains("\n  ^", formatted.Message);
    }

    [Fact]
    public void CreateType_CompositePreservesReferencesAndCollation()
    {
        var statement = Assert.IsType<CreateType.Composite>(Assert.Single(ParseStatements("CREATE TYPE app.address AS (street TEXT COLLATE app.collation, postcode app.postal_code)")));

        Assert.Equal(2, statement.Attributes.Count);
        CompositeTypeField street = statement.Attributes[0];
        Assert.Equal("street", street.Name.Name);
        Assert.IsType<DataType.Text>(Assert.IsType<DataTypeReference.BuiltIn>(street.DataType).DataType);
        Assert.Equal("app.collation", street.Collation?.ToSql());
        CompositeTypeField postcode = statement.Attributes[1];
        Assert.Equal("app.postal_code", Assert.IsType<DataTypeReference.Named>(postcode.DataType).Name.ToSql());
        Assert.NotNull(postcode.Name.Source);
        Assert.Null(postcode.Collation);
    }

    [Theory]
    [InlineData("CREATE INDEX ix ON items USING BTREE (id)")]
    [InlineData("CREATE UNIQUE INDEX ix ON items USING BTREE (id)")]
    [InlineData("CREATE INDEX ix USING BTREE ON items (id)")]
    public void CreateIndex_PreservesDialectMethodPlacement(string sql)
    {
        Assert.Equal(sql, ParseStatement(sql).ToSql());
    }

    [Theory]
    [InlineData("CREATE TYPE")]
    [InlineData("CREATE TYPE mood")]
    [InlineData("CREATE TYPE mood ENUM ('happy')")]
    [InlineData("CREATE TYPE mood AS ENUM (happy)")]
    [InlineData("CREATE TYPE mood AS ENUM (1)")]
    [InlineData("CREATE TYPE mood AS ENUM ('happy',)")]
    [InlineData("CREATE TYPE mood AS ENUM ('happy'")]
    [InlineData("CREATE TYPE address AS (street)")]
    [InlineData("CREATE TYPE address AS (street TEXT,)")]
    [InlineData("CREATE TYPE address AS (street TEXT COLLATE)")]
    [InlineData("CREATE TYPE address AS (postcode app.)")]
    [InlineData("CREATE TYPE mood AS RANGE (SUBTYPE = INT)")]
    [InlineData("CREATE OR REPLACE TYPE mood AS ENUM ('happy')")]
    [InlineData("CREATE OR ALTER TYPE mood AS ENUM ('happy')")]
    [InlineData("CREATE TEMPORARY TYPE mood AS ENUM ('happy')")]
    [InlineData("CREATE AGGREGATE TYPE mood AS ENUM ('happy')")]
    [InlineData("CREATE DEFINER = 'user'@'host' TYPE mood AS ENUM ('happy')")]
    [InlineData("CREATE ALGORITHM = MERGE TABLE sample (id INT)")]
    public void CreateType_MalformedOrUnsupportedFormsThrow(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("CREATE MATERIALIZED VIEW summary AS SELECT 1")]
    [InlineData("CREATE MATERIALIZED VIEW IF NOT EXISTS summary (id) AS VALUES (1) WITH NO DATA")]
    [InlineData("CREATE MATERIALIZED VIEW app.summary USING heap WITH (fillfactor = 70) TABLESPACE fast AS SELECT id FROM app.items WITH DATA")]
    public void MaterializedViews_CoreParserRoundTrips(string sql)
    {
        var statement = Assert.IsType<CreateMaterializedView>(ParseStatement(sql));

        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
    }

    [Theory]
    [InlineData("CREATE OR REPLACE MATERIALIZED VIEW summary AS SELECT 1")]
    [InlineData("CREATE OR ALTER MATERIALIZED VIEW summary AS SELECT 1")]
    [InlineData("CREATE TEMPORARY MATERIALIZED VIEW summary AS SELECT 1")]
    public void MaterializedViews_CoreParserRejectsUnsupportedOptions(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("REFRESH MATERIALIZED VIEW app.summary")]
    [InlineData("REFRESH MATERIALIZED VIEW CONCURRENTLY app.summary")]
    [InlineData("REFRESH MATERIALIZED VIEW app.summary WITH DATA")]
    [InlineData("REFRESH MATERIALIZED VIEW app.summary WITH NO DATA")]
    [InlineData("REFRESH MATERIALIZED VIEW CONCURRENTLY app.summary WITH DATA")]
    public void RefreshMaterializedViews_CoreParserRoundTrips(string sql)
    {
        var statement = Assert.IsType<RefreshMaterializedView>(ParseStatement(sql));

        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
    }

    [Fact]
    public void RefreshMaterializedViews_CoreParserRejectsConcurrentNoData()
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements("REFRESH MATERIALIZED VIEW CONCURRENTLY summary WITH NO DATA"));
    }

    [Theory]
    [InlineData("DROP MATERIALIZED VIEW app.summary")]
    [InlineData("DROP MATERIALIZED VIEW IF EXISTS summary")]
    [InlineData("DROP MATERIALIZED VIEW app.summary, app.other CASCADE")]
    [InlineData("DROP MATERIALIZED VIEW summary RESTRICT")]
    public void DropMaterializedViews_CoreParserRoundTrips(string sql)
    {
        var statement = Assert.IsType<DropObject>(ParseStatement(sql));

        Assert.Equal(DroppableObject.MaterializedView, statement.ObjectType);
        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
    }

    [Fact]
    public void DropMaterializedViews_LeaveOrdinaryViewIdentityUnchanged()
    {
        var statement = Assert.IsType<DropObject>(ParseStatement("DROP VIEW app.summary"));

        Assert.Equal(DroppableObject.View, statement.ObjectType);
        Assert.Null(statement.PgBehavior);
        Assert.False(statement.IfExists);
        Assert.False(statement.Temporary);
        Assert.Null(statement.OnObject);
        Assert.Equal("DROP VIEW app.summary", statement.ToSql());
    }

    [Fact]
    public void DropMaterializedViews_CoreParserRejectsTemporaryModifier()
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements("DROP TEMPORARY MATERIALIZED VIEW summary"));
    }

    [Theory]
    [InlineData("ALTER MATERIALIZED VIEW app.summary RENAME TO summary_next")]
    [InlineData("ALTER MATERIALIZED VIEW IF EXISTS summary RENAME COLUMN id TO item_id")]
    [InlineData("ALTER MATERIALIZED VIEW summary RENAME id TO item_id")]
    [InlineData("ALTER MATERIALIZED VIEW summary SET SCHEMA archive")]
    [InlineData("ALTER MATERIALIZED VIEW summary SET TABLESPACE fast")]
    [InlineData("ALTER MATERIALIZED VIEW summary SET (fillfactor = 70, autovacuum_enabled)")]
    [InlineData("ALTER MATERIALIZED VIEW IF EXISTS summary RESET (fillfactor, toast.autovacuum_enabled)")]
    [InlineData("ALTER MATERIALIZED VIEW summary OWNER TO owner_name")]
    [InlineData("ALTER MATERIALIZED VIEW IF EXISTS app.summary OWNER TO CURRENT_ROLE")]
    [InlineData("ALTER MATERIALIZED VIEW summary OWNER TO CURRENT_USER")]
    [InlineData("ALTER MATERIALIZED VIEW summary OWNER TO SESSION_USER")]
    [InlineData("ALTER MATERIALIZED VIEW summary OWNER TO \"CURRENT_USER\"")]
    [InlineData("ALTER MATERIALIZED VIEW summary SET ACCESS METHOD heap")]
    [InlineData("ALTER MATERIALIZED VIEW IF EXISTS summary SET ACCESS METHOD DEFAULT")]
    [InlineData("ALTER MATERIALIZED VIEW summary CLUSTER ON summary_idx")]
    [InlineData("ALTER MATERIALIZED VIEW summary SET WITHOUT CLUSTER")]
    [InlineData("ALTER MATERIALIZED VIEW summary DEPENDS ON EXTENSION ext")]
    [InlineData("ALTER MATERIALIZED VIEW summary NO DEPENDS ON EXTENSION ext")]
    [InlineData("ALTER MATERIALIZED VIEW summary ALTER id SET STATISTICS DEFAULT")]
    [InlineData("ALTER MATERIALIZED VIEW summary ALTER id SET (n_distinct = -0.5)")]
    [InlineData("ALTER MATERIALIZED VIEW summary ALTER id RESET (n_distinct)")]
    [InlineData("ALTER MATERIALIZED VIEW summary ALTER id SET STORAGE PLAIN")]
    [InlineData("ALTER MATERIALIZED VIEW summary ALTER id SET COMPRESSION DEFAULT")]
    [InlineData("ALTER MATERIALIZED VIEW summary SET TABLESPACE fast, ALTER id SET STATISTICS 100")]
    public void AlterMaterializedViews_CoreParserRoundTrips(string sql)
    {
        var statement = Assert.IsType<AlterMaterializedView>(ParseStatement(sql));

        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
    }

    [Fact]
    public void AlterMaterializedViews_LeaveAlterTableDispatchUnchanged()
    {
        var statement = Assert.IsType<AlterTable>(ParseStatement("ALTER TABLE items ADD COLUMN label TEXT"));

        Assert.Equal("items", statement.Table.ToSql());
        Assert.IsType<AlterTableOperation.AddColumn>(Assert.Single(statement.Operations));
        Assert.Equal("ALTER TABLE items ADD COLUMN label TEXT", statement.ToSql());
    }

    [Fact]
    public void BulkMaterializedViews_CoreParserRoundTrips()
    {
        const string sql = "ALTER MATERIALIZED VIEW ALL IN TABLESPACE fast OWNED BY owner_name, CURRENT_ROLE SET TABLESPACE slow NOWAIT";
        var statement = Assert.IsType<AlterMaterializedViewsInTablespace>(ParseStatement(sql));

        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
    }

    [Theory]
    [InlineData("CREATE SEQUENCE app.ids AS SMALLINT START WITH 10 INCREMENT BY 2 OWNED BY app.items.id")]
    [InlineData("CREATE TEMPORARY SEQUENCE IF NOT EXISTS ids NO MINVALUE NO MAXVALUE CACHE 5 NO CYCLE")]
    [InlineData("CREATE UNLOGGED SEQUENCE ids")]
    [InlineData("ALTER SEQUENCE ids RESTART WITH -5 CACHE 2")]
    [InlineData("ALTER SEQUENCE ids OWNER TO CURRENT_ROLE")]
    [InlineData("DROP SEQUENCE IF EXISTS ids, app.more_ids CASCADE")]
    [InlineData("ALTER TABLE IF EXISTS ONLY app.items ALTER COLUMN id SET GENERATED ALWAYS RESTART WITH 10")]
    public static void SequenceSyntax_PreservesKitchenSinkGrammar(string sql)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.Parse, sql);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void SequenceOptions_CollectOptionsFromVirtualHook(bool alter)
    {
        var state = GetState("SEQUENCE ids RESET CACHE 2");
        var parser = new ExtendedSequenceDdlParser(new Parser());
        SqlValueList<PgStatementSequenceOption> options;
        if (alter)
        {
            AlterSequence statement = parser.ParseAlterSequence(state);
            options = Assert.IsType<PgAlterSequenceOperation.Parameters>(statement.Operation).Options;
        }
        else
        {
            options = parser.ParseCreateSequence(state, null, false).Options;
        }

        Assert.Equal(2, options.Count);
        Assert.False(Assert.IsType<PgStatementSequenceOption.Cycle>(options[0]).Enabled);
        Assert.Equal(2, Assert.IsType<PgStatementSequenceOption.Cache>(options[1]).Value);
        Assert.True(state.PeekIs<Core.Lexing.Tokens.Eof>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void SequenceOptions_DetectDuplicateOptionsFromVirtualHook(bool alter)
    {
        var state = GetState("SEQUENCE ids RESET CYCLE");
        var parser = new ExtendedSequenceDdlParser(new Parser());

        if (alter)
        {
            Assert.Throws<ParseException.Duplicate>(() => parser.ParseAlterSequence(state));
        }
        else
        {
            Assert.Throws<ParseException.Duplicate>(() => parser.ParseCreateSequence(state, null, false));
        }
    }

    [Theory]
    [InlineData("AS")]
    [InlineData("OWNED")]
    [InlineData("OWNED NONE")]
    [InlineData("OWNED BY")]
    [InlineData("NO")]
    public static void SequenceOptions_RejectMalformedRecognizedClauses(string clause)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatement($"CREATE SEQUENCE ids {clause}"));
    }

    private sealed class ExtendedSequenceDdlParser(Parser parser) : DdlParser(parser)
    {
        protected override PgStatementSequenceOption? ParseNextSequenceOption(ParserState state, bool alter)
        {
            if (state.ParseKeyword(Keyword.RESET))
            {
                return new PgStatementSequenceOption.Cycle(false);
            }
            return base.ParseNextSequenceOption(state, alter);
        }
    }
}
