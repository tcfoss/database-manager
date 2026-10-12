using System.Diagnostics;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.Components;
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

    [Theory]
    [InlineData("'myname'@'myhost'", QuoteStyle.Backticks, ExtendedQuoteStyle.Backticks, "myname", "myhost")]
    [InlineData("'myname'", QuoteStyle.Backticks, ExtendedQuoteStyle.Backticks, "myname", null)]
    [InlineData("`myname`@`myhost`", QuoteStyle.Backticks, ExtendedQuoteStyle.Backticks, "myname", "myhost")]
    [InlineData("`myname`", QuoteStyle.Backticks, ExtendedQuoteStyle.Backticks, "myname", null)]
    [InlineData("'myname'@'myhost'", QuoteStyle.Ansi, ExtendedQuoteStyle.Ansi, "myname", "myhost")]
    [InlineData("'myname'@'myhost'", QuoteStyle.Brackets, ExtendedQuoteStyle.Brackets, "myname", "myhost")]
    [InlineData("'myname'@'myhost'", QuoteStyle.None, ExtendedQuoteStyle.None, "myname", "myhost")]
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
        Assert.Throws<UnreachableException>(() => repoHelper.ParseAccount("'myname'@'myhost'"));
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
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("NOT AN ON UPDATE", true)]
    public void OnUpdateCurrentTimestamp(string? given, bool expectNull)
    {
        var repoHelper = new InfoSchemaRepoHelper(_lexer, _parser, QuoteStyle.Backticks);
        var actual = repoHelper.ParseOnUpdate(given);

        ColumnOption.OnUpdate? expected = expectNull ? null : new ColumnOption.OnUpdate(
            new FunctionCall(
                new ObjectName(new Identifier("CURRENT_TIMESTAMP")))
            {
                Arguments = FunctionArguments.EmptyList()
            });

        Assert.Equal(expected, actual);
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
                    new KeyPart.Column(new Identifier("first")),
                    new KeyPart.Column(new Identifier("second"))
                ],
                new Identifier("my_index"))
            {
                IndexMethod = IndexMethod.Btree,
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
                [new KeyPart.Column(new Identifier("body"))],
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
                [new KeyPart.Column(new Identifier("location"))],
                new Identifier("location_index")));

        Assert.Equal(expected, actual);
    }
}
