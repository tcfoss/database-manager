using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Expressions.Components;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.MySql.DatabaseComms;
using TcfOss.DatabaseManager.MySql.DatabaseComms.InfoSchemaHelperModels;
using TcfOss.DatabaseManager.MySql.DatabaseObjects.Components;
using TcfOss.DatabaseManager.MySql.Lexing;
using TcfOss.DatabaseManager.MySql.Parsing;

namespace TcfOss.DatabaseManager.MySql.Tests.DatabaseComms;

public class InfoSchemaRepoHelperTests
{
    private readonly MyLexer _lexer = new();
    private readonly MyParser _parser = new();

    [Fact]
    public void ExpressionParsing_RejectsTrailingTokens()
    {
        var repoHelper = new InfoSchemaRepoHelper(_lexer, _parser, QuoteStyle.Backticks);

        Assert.Throws<InvalidOperationException>(() => repoHelper.ParseExpr("value unexpected"));
        Assert.Throws<InvalidOperationException>(() => repoHelper.ParseColumnDefault("1 unexpected", false));
        Assert.Throws<ParseException.ExpectedButFound>(() => repoHelper.ParseOnUpdate("ON UPDATE value unexpected"));
        Assert.Throws<ParseException.ExpectedButFound>(() => repoHelper.ParseOnUpdate("DEFAULT_GENERATED on update CURRENT_TIMESTAMP unexpected"));
        var expression = Assert.IsType<ColumnOption.ColumnDefault.DefaultExpression>(repoHelper.ParseColumnDefault("1 + 2", false));
        Assert.Equal("1 + 2", expression.Expression.ToSql());
    }

    [Theory]
    [InlineData("'myname'@'myhost'", QuoteStyle.Backticks, ExtendedQuoteStyle.Backticks, "myname", "myhost")]
    [InlineData("'myname'", QuoteStyle.Backticks, ExtendedQuoteStyle.Backticks, "myname", null)]
    [InlineData("`myname`@`myhost`", QuoteStyle.Backticks, ExtendedQuoteStyle.Backticks, "myname", "myhost")]
    [InlineData("`myname`", QuoteStyle.Backticks, ExtendedQuoteStyle.Backticks, "myname", null)]
    [InlineData("'myname'@'myhost'", QuoteStyle.Ansi, ExtendedQuoteStyle.Ansi, "myname", "myhost")]
    [InlineData("'myname'@'myhost'", QuoteStyle.Brackets, ExtendedQuoteStyle.Brackets, "myname", "myhost")]
    [InlineData("'myname'@'myhost'", QuoteStyle.None, ExtendedQuoteStyle.None, "myname", "myhost")]
    [InlineData("myname@%", QuoteStyle.Backticks, ExtendedQuoteStyle.Backticks, "myname", "%")]
    [InlineData("'myname'@%", QuoteStyle.Backticks, ExtendedQuoteStyle.Backticks, "myname", "%")]
    public void AccountsParse(string given, QuoteStyle quoteStyle, ExtendedQuoteStyle expectedQuoteStyle, string expectedIdentity, string? expectedHost)
    {
        Account expected;
        if (expectedHost != null)
        {
            expected = new Account.IdentityWithHost(
                new ExtendedIdentifier(expectedIdentity, expectedQuoteStyle),
                new ExtendedIdentifier(expectedHost, expectedQuoteStyle));
        }
        else
        {
            expected = new Account.Identity(
                new ExtendedIdentifier(expectedIdentity, expectedQuoteStyle));
        }


        var repoHelper = new InfoSchemaRepoHelper(_lexer, _parser, quoteStyle);

        var actual = repoHelper.ParseAccount(given);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AccountsParse_CurrentUser()
    {
        var repoHelper = new InfoSchemaRepoHelper(_lexer, _parser, QuoteStyle.Backticks);
        var actual = repoHelper.ParseAccount("CURRENT_USER");
        var expected = new Account.CurrentUser();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AccountsParse_UnknownQuoteStyle_Throws()
    {
        var repoHelper = new InfoSchemaRepoHelper(_lexer, _parser, (QuoteStyle)999);
        Assert.Throws<DefensiveGuardException>(() => repoHelper.ParseAccount("'myname'@'myhost'"));
    }

    [Theory]
    [InlineData("INT", true)]
    [InlineData("INT SIGNED", true)]
    [InlineData("INT UNSIGNED", false)]
    [InlineData("INT ZEROFILL", false)]
    [InlineData("DECIMAL(10, 2)", true)]
    [InlineData("DECIMAL(10, 2) SIGNED", true)]
    [InlineData("DECIMAL(10, 2) UNSIGNED", false)]
    [InlineData("DECIMAL(10, 2) ZEROFILL", false)]
    [InlineData("DOUBLE", true)]
    [InlineData("DOUBLE SIGNED", true)]
    [InlineData("DOUBLE UNSIGNED", false)]
    [InlineData("DOUBLE ZEROFILL", false)]
    [InlineData("FLOAT", true)]
    [InlineData("FLOAT SIGNED", true)]
    [InlineData("FLOAT UNSIGNED", false)]
    [InlineData("FLOAT ZEROFILL", false)]
    public void AllNumericsDefaultToSigned(string dataType, bool expectedSigned)
    {
        var repoHelper = new InfoSchemaRepoHelper(_lexer, _parser, QuoteStyle.Backticks);
        var actual = repoHelper.ParseDataTypeWithDefaults(dataType, null, null);
        string actualString = actual.ToSql();
        if (expectedSigned)
        {
            Assert.Contains("SIGNED", actualString);
            Assert.DoesNotContain("UNSIGNED", actualString);
        }
        else
        {
            if (actualString.Contains("SIGNED"))
            {
                Assert.Contains("UNSIGNED", actualString);
            }
            else
            {
                Assert.DoesNotContain("SIGNED", actualString);
            }
        }
    }

    [Theory]
    [InlineData("VARCHAR(255)", "VARCHAR(255) CHARACTER SET overridecharset COLLATE overridecollation")]
    [InlineData("VARCHAR(255) CHARSET utf8mb4", "VARCHAR(255) CHARACTER SET utf8mb4")]
    [InlineData("VARCHAR(255) COLLATE utf8mb4_general_ci", "VARCHAR(255) COLLATE utf8mb4_general_ci")]
    public void StringTypesTakeDefaults(string dataType, string expected)
    {
        var repoHelper = new InfoSchemaRepoHelper(_lexer, _parser, QuoteStyle.Backticks);
        var actual = repoHelper.ParseDataTypeWithDefaults(dataType, "overridecharset", "overridecollation");
        Assert.Equal(expected, actual.ToSql());
    }

    [Theory]
    [InlineData("ON UPDATE CURRENT_TIMESTAMP", false)]
    [InlineData("DEFAULT_GENERATED on update CURRENT_TIMESTAMP", false)]
    [InlineData("default_generated ON UPDATE CURRENT_TIMESTAMP", false)]
    [InlineData("DEFAULT_GENERATED", true)]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("NOT AN ON UPDATE", true)]
    public void OnUpdateCurrentTimestamp(string? given, bool expectNull)
    {
        var repoHelper = new InfoSchemaRepoHelper(_lexer, _parser, QuoteStyle.Backticks);
        var actual = repoHelper.ParseOnUpdate(given);

        ColumnOption.OnUpdate? expected = expectNull
            ? null
            : new ColumnOption.OnUpdate(
                new FunctionCall(
                    new ObjectName(new Identifier("CURRENT_TIMESTAMP")))
                {
                    Arguments = new FunctionArguments.None()
                });

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void OnUpdateCurrentTimestamp_WithPrecision()
    {
        var repoHelper = new InfoSchemaRepoHelper(_lexer, _parser, QuoteStyle.Backticks);

        var actual = repoHelper.ParseOnUpdate("DEFAULT_GENERATED on update CURRENT_TIMESTAMP(6)");

        Assert.Equal(new ColumnOption.OnUpdate(repoHelper.ParseExpr("CURRENT_TIMESTAMP(6)")), actual);
    }

    [Fact]
    public void IndexInfoToKeyCreatesStandardKeyWithSortedColumnsAndComment()
    {
        var repoHelper = new InfoSchemaRepoHelper(_lexer, _parser, QuoteStyle.None);
        List<IndexInfo> info =
        [
            new()
            {
                ColumnName = new Identifier("second"),
                IndexName = "my_index",
                IndexType = "BTREE",
                IndexComment = "index comment",
                SeqInIndex = 2
            },
            new()
            {
                ColumnName = new Identifier("first"),
                IndexName = "my_index",
                IndexType = "BTREE",
                IndexComment = "index comment",
                SeqInIndex = 1
            }
        ];

        KeyValuePair<Handle, MyKey> actual = repoHelper.IndexInfoToKey(info, NameHandling.Uppercase);

        var expected = new KeyValuePair<Handle, MyKey>(
            Handle.Create(new Identifier("my_index"), NameHandling.Uppercase),
            new MyKey.Standard(
                [
                    new MyKeyPart.Column(new Identifier("first")),
                    new MyKeyPart.Column(new Identifier("second"))
                ],
                new Identifier("my_index"))
            {
                IndexMethod = BuiltInIndexMethod.Btree,
                Comment = new Comment("index comment")
            });

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void IndexInfoToKeyCreatesFullTextKey()
    {
        var repoHelper = new InfoSchemaRepoHelper(_lexer, _parser, QuoteStyle.None);
        List<IndexInfo> info =
        [
            new()
            {
                ColumnName = new Identifier("body"),
                IndexName = "body_search",
                IndexType = "FULLTEXT",
                IndexComment = "",
                SeqInIndex = 1
            }
        ];

        KeyValuePair<Handle, MyKey> actual = repoHelper.IndexInfoToKey(info, NameHandling.None);

        var expected = new KeyValuePair<Handle, MyKey>(
            Handle.Create(new Identifier("body_search"), NameHandling.None),
            new MyKey.FullText(
                [new MyKeyPart.Column(new Identifier("body"))],
                new Identifier("body_search")));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void IndexInfoToKeyCreatesSpatialKey()
    {
        var repoHelper = new InfoSchemaRepoHelper(_lexer, _parser, QuoteStyle.None);
        List<IndexInfo> info =
        [
            new()
            {
                ColumnName = new Identifier("location"),
                IndexName = "location_index",
                IndexType = "SPATIAL",
                IndexComment = "",
                SeqInIndex = 1
            }
        ];

        KeyValuePair<Handle, MyKey> actual = repoHelper.IndexInfoToKey(info, NameHandling.None);

        var expected = new KeyValuePair<Handle, MyKey>(
            Handle.Create(new Identifier("location_index"), NameHandling.None),
            new MyKey.Spatial(
                [new MyKeyPart.Column(new Identifier("location"))],
                new Identifier("location_index")));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("VARCHAR(20)", "default")]
    [InlineData("VARCHAR(20)", "lower(`base_col`)")]
    [InlineData("VARCHAR(20)", "(base_col)")]
    [InlineData("VARCHAR(20)", "NULL")]
    [InlineData("VARCHAR(20)", "123")]
    [InlineData("VARCHAR(20)", "")]
    [InlineData("VARCHAR(20)", "a''b")]
    [InlineData("VARCHAR(20)", "a'b\\c")]
    [InlineData("DATE", "2024-01-01")]
    [InlineData("DATETIME(6)", "2024-01-01 12:34:56.123456")]
    [InlineData("TIMESTAMP", "2024-01-01 12:34:56")]
    [InlineData("TIME(6)", "-12:34:56.123456")]
    [InlineData("ENUM('a', 'b')", "a")]
    [InlineData("SET('a', 'b')", "a,b")]
    public void ColumnDefault_PreservesUnquotedLiteral(string dataType, string text)
    {
        var repoHelper = new InfoSchemaRepoHelper(_lexer, _parser, QuoteStyle.Backticks);

        ColumnOption.ColumnDefault? actual = repoHelper.ParseColumnDefault(text, false, repoHelper.ParseDataType(dataType), "", false);

        Assert.Equal(new ColumnOption.ColumnDefault.DefaultValue(new Value.SingleQuotedString(text)), actual);
    }

    [Theory]
    [InlineData("\nline", @"'\nline'")]
    [InlineData("it's", @"'it\'s'")]
    [InlineData("it's", "'it''s'")]
    [InlineData("a\\b", @"'a\\b'")]
    [InlineData("\\'x", @"'\\\'x'")]
    [InlineData("\\%", @"'\%'")]
    [InlineData("\0\b\r\t\u001a", @"'\0\b\r\t\Z'")]
    public void ColumnDefault_MySqlLiteralMatchesSqlLiteral(string databaseValue, string sqlLiteral)
    {
        var helper = new InfoSchemaRepoHelper(_lexer, _parser, QuoteStyle.Backticks);

        var databaseDefault = Assert.IsType<ColumnOption.ColumnDefault.DefaultValue>(
            helper.ParseColumnDefault(databaseValue, false, helper.ParseDataType("VARCHAR(20)"), "", false));
        var sqlDefault = Assert.IsType<ColumnOption.ColumnDefault.DefaultValue>(helper.ParseColumnDefault(sqlLiteral, false));

        Assert.Equal(sqlDefault, databaseDefault);
        Assert.Equal(sqlDefault.GetHashCode(), databaseDefault.GetHashCode());
        Assert.Equal(databaseDefault, helper.ParseColumnDefault(databaseDefault.Value.ToSql(), false));
    }

    [Fact]
    public void ColumnDefault_LiteralBackslashNDoesNotEqualNewline()
    {
        var helper = new InfoSchemaRepoHelper(_lexer, _parser, QuoteStyle.Backticks);
        var dataType = helper.ParseDataType("VARCHAR(20)");

        var newline = helper.ParseColumnDefault("\n", false, dataType, "", false);
        var backslashN = helper.ParseColumnDefault("\\n", false, dataType, "", false);

        Assert.NotEqual(newline, backslashN);
    }

    [Theory]
    [InlineData("VARCHAR(20)", "lower(`base_col`)", "DEFAULT_GENERATED")]
    [InlineData("VARCHAR(20)", "`base_col`", "DEFAULT_GENERATED")]
    [InlineData("VARCHAR(20)", "base_col", "default_generated")]
    [InlineData("VARCHAR(20)", "'constant'", "DEFAULT_GENERATED")]
    [InlineData("INT", "1 + 2", "DEFAULT_GENERATED")]
    [InlineData("VARBINARY(20)", "uuid_to_bin(uuid())", "DEFAULT_GENERATED")]
    [InlineData("TIMESTAMP(6)", "CURRENT_TIMESTAMP(6)", "DEFAULT_GENERATED on update CURRENT_TIMESTAMP(6)")]
    public void ColumnDefault_UsesExtraToRecognizeExpressions(string dataType, string text, string extra)
    {
        var repoHelper = new InfoSchemaRepoHelper(_lexer, _parser, QuoteStyle.Backticks);

        ColumnOption.ColumnDefault? actual = repoHelper.ParseColumnDefault(text, false, repoHelper.ParseDataType(dataType), extra, false);

        Assert.Equal(new ColumnOption.ColumnDefault.DefaultExpression(repoHelper.ParseExpr(text)), actual);
    }

    [Fact]
    public void ColumnDefault_RemovesSlashesBeforeQuotesWhenRequested()
    {
        var repoHelper = new InfoSchemaRepoHelper(_lexer, _parser, QuoteStyle.Backticks);

        ColumnOption.ColumnDefault? actual = repoHelper.ParseColumnDefault("concat(\\'a\\', `base_col`)", false, repoHelper.ParseDataType("VARCHAR(20)"), "DEFAULT_GENERATED", true);

        Assert.Equal(new ColumnOption.ColumnDefault.DefaultExpression(repoHelper.ParseExpr("concat('a', `base_col`)")), actual);
    }

    [Theory]
    [InlineData("INT", "42")]
    [InlineData("INT", "-42")]
    [InlineData("DECIMAL(10,2)", "12.34")]
    [InlineData("DOUBLE", "1.25")]
    [InlineData("YEAR", "2024")]
    public void ColumnDefault_PreservesNumericParsing(string dataType, string text)
    {
        var repoHelper = new InfoSchemaRepoHelper(_lexer, _parser, QuoteStyle.Backticks);

        ColumnOption.ColumnDefault? actual = repoHelper.ParseColumnDefault(text, false, repoHelper.ParseDataType(dataType), "", false);

        Assert.Equal(repoHelper.ParseColumnDefault(text, false), actual);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ColumnDefault_PreservesNullHandling(bool isNullable)
    {
        var repoHelper = new InfoSchemaRepoHelper(_lexer, _parser, QuoteStyle.Backticks);

        ColumnOption.ColumnDefault? actual = repoHelper.ParseColumnDefault(null, isNullable, repoHelper.ParseDataType("VARCHAR(20)"), "", false);

        Assert.Equal(repoHelper.ParseColumnDefault(null, isNullable), actual);
    }

    [Theory]
    [InlineData("VARBINARY(20)", "0x64656661756C745F736563726574", "64656661756C745F736563726574")]
    [InlineData("BINARY(4)", "0x61626300", "61626300")]
    [InlineData("VARBINARY(20)", "0x", "")]
    public void ColumnDefault_ParsesBinaryHexLiteral(string dataType, string text, string expectedHex)
    {
        var repoHelper = new InfoSchemaRepoHelper(_lexer, _parser, QuoteStyle.Backticks);

        ColumnOption.ColumnDefault? actual = repoHelper.ParseColumnDefault(text, false, repoHelper.ParseDataType(dataType), "", false);

        Assert.Equal(new ColumnOption.ColumnDefault.DefaultValue(new Value.HexString(expectedHex)), actual);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("VIRTUAL GENERATED", false)]
    [InlineData("stored generated", true)]
    public void GeneratedColumn_ParsesExpressionAndStorageMode(string extra, bool stored)
    {
        var helper = new InfoSchemaRepoHelper(_lexer, _parser, QuoteStyle.Backticks);

        var actual = helper.ParseGenerated("CONCAT(\\'prefix\\', value)", extra, true);

        Assert.Equal(new ColumnOption.Generated.AsExpression(helper.ParseExpr("CONCAT('prefix', value)"),
            stored ? GenerationMode.Stored : GenerationMode.Virtual), actual);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public void OptionalColumnExpressions_AbsentTextProducesNoClause(string? text)
    {
        var helper = new InfoSchemaRepoHelper(_lexer, _parser, QuoteStyle.Backticks);

        Assert.Null(helper.ParseGenerated(text, "", false));
        Assert.Null(helper.ParseColumnCheck(text));
    }

    [Fact]
    public void ColumnCheck_ParsesExpression()
    {
        var helper = new InfoSchemaRepoHelper(_lexer, _parser, QuoteStyle.Backticks);

        Assert.Equal(new ColumnOption.CheckConstraint(helper.ParseExpr("amount > 0")), helper.ParseColumnCheck("amount > 0"));
    }

    [Theory]
    [InlineData(false, "VALUES ({0}),({1})")]
    [InlineData(true, "VALUES ROW({0}),ROW({1})")]
    public void UnappliedEntriesSql_EscapesSchemasAndPreservesParameterSlots(bool includeRowKeyword, string firstLine)
    {
        var catalog = new CatalogIdentifier("def");
        SchemaIdentifier[] schemas = [new("first`{schema}", catalog), new("second", catalog)];

        string sql = InfoSchemaRepoHelper.GetUnappliedEntriesSql(schemas, 2, "R", includeRowKeyword);

        Assert.StartsWith(firstLine + Environment.NewLine, sql);
        Assert.Contains("`first``{{schema}}`.", sql);
        Assert.Contains("UNION ALL" + Environment.NewLine, sql);
        Assert.Contains("FROM `second`.", sql);
        Assert.Equal(2, sql.Split(" = 'R'").Length - 1);
    }

    [Fact]
    public void TriggerMetadata_PreservesGroupingOrderAndParsedBody()
    {
        var helper = new InfoSchemaRepoHelper(_lexer, _parser, QuoteStyle.Backticks);
        var table = new ObjectIdentifier("items", new SchemaIdentifier("app", new CatalogIdentifier("def")));
        TriggerDto first = new()
        {
            EventObjectTable = "items",
            TriggerName = "first",
            EventManipulation = "INSERT",
            ActionTiming = "BEFORE",
            ActionStatement = "SET NEW.value = 1",
            ActionOrder = 1,
            Definer = "owner@localhost"
        };
        TriggerDto[] rows =
        [
            first,
            first with { TriggerName = "second", ActionOrder = 2 },
            first with { TriggerName = "after_insert", ActionTiming = "AFTER", ActionOrder = 1 }
        ];

        var triggers = helper.TriggersOnTableToDictionary(rows, table);

        Assert.Equal(3, triggers.Count);
        foreach (TriggerDto row in rows)
        {
            var trigger = triggers[new ObjectIdentifier(row.TriggerName, table.Schema, QuoteStyle.Backticks)];
            Assert.Equal((uint)row.ActionOrder, trigger.Order);
            Assert.Equal(TriggerTime.Parse(row.ActionTiming), trigger.TriggerTime);
            Assert.Equal(TriggerEvent.Parse(row.EventManipulation), trigger.TriggerEvent);
            Assert.Equal(row.ActionStatement, trigger.RawBodyText);
            Assert.Equal(helper.ParseStatement(row.ActionStatement), trigger.Body);
        }
    }

    [Theory]
    [InlineData("BTREE", "")]
    [InlineData("HASH", "owner's index")]
    public void IndexMetadata_ParsesMethodAndPreservesOptionalComment(string indexType, string comment)
    {
        var row = new IndexInfo
        {
            ColumnName = new Identifier("value"),
            IndexType = indexType,
            IndexComment = comment,
            IndexName = "index_value",
            SeqInIndex = 1
        };

        var (method, actualComment) = row.GetIndexTypeAndComment(_lexer, _parser);

        Assert.Equal(BuiltInIndexMethod.Parse(indexType), method);
        Assert.Equal(comment.Length == 0 ? null : new Comment(comment), actualComment);
    }

}
