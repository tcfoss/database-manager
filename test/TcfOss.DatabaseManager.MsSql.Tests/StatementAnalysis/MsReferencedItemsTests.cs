using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;
using TcfOss.DatabaseManager.Core.Tests;
using TcfOss.DatabaseManager.MsSql.BuiltIn;
using TcfOss.DatabaseManager.MsSql.Lexing;
using TcfOss.DatabaseManager.MsSql.Parsing;
using TcfOss.DatabaseManager.MsSql.Statements;

namespace TcfOss.DatabaseManager.MsSql.Tests.StatementAnalysis;

public class MsReferencedItemsTests
{
    [Fact]
    public void MsExecute_WithProcedure()
    {
        var text = "EXECUTE myproc @param1, @param2";
        var stmt = ParseStatement(text);

        Assert.IsType<MsExecute.ProcedureCall>(stmt);

        var items = stmt.GetReferencedItems(CreateReferencedItemsManager()).ToList();

        Assert.Equal(3, items.Count);
        Assert.Equal(ItemType.Procedure, items[0].Type);
        Assert.Equal("myproc", items[0].N());
        Assert.Equal(ItemType.Variable, items[1].Type);
        Assert.Equal("param1", items[1].N());
        Assert.Equal(ItemType.Variable, items[2].Type);
        Assert.Equal("param2", items[2].N());
    }

    [Theory]
    [InlineData("EXEC myproc2 @param1, @param2")]
    [InlineData("EXECUTE myproc2 @param1, @param2")]
    public void MsExecute_NonExistentProcedure(string text)
    {
        var stmt = ParseStatement(text);

        Assert.IsType<MsExecute.ProcedureCall>(stmt);

        var items = stmt.GetReferencedItems(CreateReferencedItemsManager()).ToList();

        Assert.Equal(3, items.Count);
        Assert.Equal(ItemType.Unknown, items[0].Type);
        Assert.Equal("myproc2", items[0].N());
        Assert.Equal(ItemType.Variable, items[1].Type);
        Assert.Equal("param1", items[1].N());
        Assert.Equal(ItemType.Variable, items[2].Type);
        Assert.Equal("param2", items[2].N());
    }

    [Fact]
    public void MsExecute_DynamicSql()
    {
        var text = "EXECUTE ('SELECT col1 FROM mytable')";
        var stmt = ParseStatement(text);

        Assert.IsType<MsExecute.DynamicSqlCall>(stmt);

        var items = stmt.GetReferencedItems(CreateReferencedItemsManager()).ToList();

        Assert.Empty(items);
    }

    [Fact]
    public void MsRaiseError_AllLiterals()
    {
        var text = "RAISERROR ('Error message', 16, 1)";
        var stmt = ParseStatement(text);

        Assert.IsType<MsRaiseError>(stmt);

        var items = stmt.GetReferencedItems(CreateReferencedItemsManager()).ToList();

        Assert.Empty(items);
    }

    [Fact]
    public void MsRaiseError_WithExpressionForMessage()
    {
        var text = "RAISERROR (@errMsg, 16, 1)";
        var stmt = ParseStatement(text);

        Assert.IsType<MsRaiseError>(stmt);

        var items = stmt.GetReferencedItems(CreateReferencedItemsManager()).ToList();

        var item = Assert.Single(items);
        Assert.Equal(ItemType.Variable, item.Type);
        Assert.Equal("errMsg", item.N());
    }

    [Fact]
    public void MsRaiseError_WithExpressionForSeverity()
    {
        var text = "RAISERROR ('Error message', @severity, 1)";
        var stmt = ParseStatement(text);

        Assert.IsType<MsRaiseError>(stmt);

        var items = stmt.GetReferencedItems(CreateReferencedItemsManager()).ToList();

        var item = Assert.Single(items);
        Assert.Equal(ItemType.Variable, item.Type);
        Assert.Equal("severity", item.N());
    }

    [Fact]
    public void MsRaiseError_WithExpressionForState()
    {
        var text = "RAISERROR ('Error message', 16, @state)";
        var stmt = ParseStatement(text);

        Assert.IsType<MsRaiseError>(stmt);

        var items = stmt.GetReferencedItems(CreateReferencedItemsManager()).ToList();

        var item = Assert.Single(items);
        Assert.Equal(ItemType.Variable, item.Type);
        Assert.Equal("state", item.N());
    }

    [Fact]
    public void MsThrow_AllLiterals()
    {
        var text = "THROW 50000, 'Error message', 1";
        var stmt = ParseStatement(text);

        Assert.IsType<MsThrow>(stmt);

        var items = stmt.GetReferencedItems(CreateReferencedItemsManager()).ToList();

        Assert.Empty(items);
    }

    [Fact]
    public void MsThrow_WithExpressionForMessage()
    {
        var text = "THROW 50000, @errMsg, 1";
        var stmt = ParseStatement(text);

        Assert.IsType<MsThrow>(stmt);

        var items = stmt.GetReferencedItems(CreateReferencedItemsManager()).ToList();

        var item = Assert.Single(items);
        Assert.Equal(ItemType.Variable, item.Type);
        Assert.Equal("errMsg", item.N());
    }

    [Fact]
    public void MsThrow_WithExpressionForErrorNumber()
    {
        var text = "THROW @errorNumber, 'Error message', 1";
        var stmt = ParseStatement(text);

        Assert.IsType<MsThrow>(stmt);

        var items = stmt.GetReferencedItems(CreateReferencedItemsManager()).ToList();

        var item = Assert.Single(items);
        Assert.Equal(ItemType.Variable, item.Type);
        Assert.Equal("errorNumber", item.N());
    }

    [Fact]
    public void MsThrow_WithExpressionForState()
    {
        var text = "THROW 50000, 'Error message', @state";
        var stmt = ParseStatement(text);

        Assert.IsType<MsThrow>(stmt);

        var items = stmt.GetReferencedItems(CreateReferencedItemsManager()).ToList();

        var item = Assert.Single(items);
        Assert.Equal(ItemType.Variable, item.Type);
        Assert.Equal("state", item.N());
    }

    [Fact]
    public void TryCatch()
    {
        var text = """
        BEGIN TRY
            EXECUTE myproc @param1, @param2
        END TRY
        BEGIN CATCH
            THROW 50000, @errmsg, 1;
        END CATCH
        """;
        var stmt = ParseStatement(text);

        Assert.IsType<MsTryCatch>(stmt);

        var items = stmt.GetReferencedItems(CreateReferencedItemsManager()).ToList();

        Assert.Equal(4, items.Count);
        Assert.Equal(ItemType.Procedure, items[0].Type);
        Assert.Equal("myproc", items[0].N());
        Assert.Equal(ItemType.Variable, items[1].Type);
        Assert.Equal("param1", items[1].N());
        Assert.Equal(ItemType.Variable, items[2].Type);
        Assert.Equal("param2", items[2].N());
        Assert.Equal(ItemType.Variable, items[3].Type);
        Assert.Equal("errmsg", items[3].N());
    }

    [Fact]
    public void MsIf()
    {
        var text = """
        IF @condition = 1
            EXECUTE myproc @param1, @param2
        ELSE IF @condition = 2
            RAISERROR (@errmsg, 16, 1)
        ELSE
            THROW 50000, @elsemsg, 1;
        """;

        var stmt = ParseStatement(text);

        Assert.IsType<If.Ms>(stmt);

        var items = stmt.GetReferencedItems(CreateReferencedItemsManager()).ToList();

        Assert.Equal(7, items.Count);
        Assert.Equal(ItemType.Variable, items[0].Type);
        Assert.Equal("condition", items[0].N());
        Assert.Equal(ItemType.Procedure, items[1].Type);
        Assert.Equal("myproc", items[1].N());
        Assert.Equal(ItemType.Variable, items[2].Type);
        Assert.Equal("param1", items[2].N());
        Assert.Equal(ItemType.Variable, items[3].Type);
        Assert.Equal("param2", items[3].N());
        Assert.Equal(ItemType.Variable, items[4].Type);
        Assert.Equal("condition", items[4].N());
        Assert.Equal(ItemType.Variable, items[5].Type);
        Assert.Equal("errmsg", items[5].N());
        Assert.Equal(ItemType.Variable, items[6].Type);
        Assert.Equal("elsemsg", items[6].N());
    }

    [Fact]
    public void MsDeclare_NoDefault()
    {
        var text = "DECLARE @myVar INT";
        var stmt = ParseStatement(text);

        Assert.IsType<DeclareLocalVariable.Ms>(stmt);

        var items = stmt.GetReferencedItems(CreateReferencedItemsManager()).ToList();

        Assert.Empty(items);
    }

    [Fact]
    public void MsDeclare_WithDefault()
    {
        var text = "DECLARE @myVar INT = (SELECT MAX(col1) FROM mytable)";
        var stmt = ParseStatement(text);

        Assert.IsType<DeclareLocalVariable.Ms>(stmt);

        var items = stmt.GetReferencedItems(CreateReferencedItemsManager()).ToList();

        Assert.Equal(3, items.Count);
        Assert.Equal(ItemType.Table, items[0].Type);
        Assert.Equal("mytable", items[0].N());
        Assert.Equal(ItemType.BuiltInFunction, items[1].Type);
        Assert.Equal("MAX", items[1].N());
        Assert.Equal(ItemType.TableColumn, items[2].Type);
        Assert.Equal("col1", items[2].N());
    }

    [Theory]
    [InlineData("BREAK")]
    [InlineData("CONTINUE")]
    [InlineData("GO")]
    [InlineData("COMMIT")]
    [InlineData("COMMIT TRAN")]
    [InlineData("COMMIT TRAN mytransaction")]
    [InlineData("COMMIT TRANSACTION")]
    [InlineData("COMMIT TRANSACTION mytransaction")]
    [InlineData("ROLLBACK")]
    [InlineData("ROLLBACK TRAN")]
    [InlineData("ROLLBACK TRAN mytransaction")]
    [InlineData("ROLLBACK TRANSACTION")]
    [InlineData("ROLLBACK TRANSACTION mytransaction")]
    [InlineData("SAVE TRAN mytransaction")]
    [InlineData("SAVE TRANSACTION mytransaction")]
    public void StatementsWithNoReferencedItems(string text)
    {
        var stmt = ParseStatement(text);
        var items = stmt.GetReferencedItems(CreateReferencedItemsManager()).ToList();
        Assert.Empty(items);
    }

    [Fact]
    public void SelectWithRowNumber()
    {
        var text = "SELECT col1, ROW_NUMBER() OVER (PARTITION BY col2 ORDER BY col3) AS rn FROM mytable";
        var stmt = ParseStatement(text);

        Assert.IsType<Select>(stmt);

        var items = stmt.GetReferencedItems(CreateReferencedItemsManager()).ToList();

        Assert.Equal(5, items.Count);
        Assert.Equal(ItemType.Table, items[0].Type);
        Assert.Equal("mytable", items[0].N());
        Assert.Equal(ItemType.TableColumn, items[1].Type);
        Assert.Equal("col1", items[1].N());
        Assert.Equal(ItemType.BuiltInFunction, items[2].Type);
        Assert.Equal("ROW_NUMBER", items[2].N());
        Assert.Equal(ItemType.TableColumn, items[3].Type);
        Assert.Equal("col2", items[3].N());
        Assert.Equal(ItemType.TableColumn, items[4].Type);
        Assert.Equal("col3", items[4].N());
    }

    // ===== Parse helpers =====

    private static Statement ParseStatement(string sql)
    {
        var tokens = new MsLexer().Tokenize(sql);
        var state = new ParserState([.. tokens]);
        return new MsParser().ParseStatement(state);
    }

    // ===== Data Helpers =====

    private static readonly CatalogIdentifier s_testCatalog = new("mycat", QuoteStyle.Brackets);
    private static readonly SchemaIdentifier s_testSchema = new("dbo", s_testCatalog, QuoteStyle.Brackets);

    private static ReferencedItemsManager CreateReferencedItemsManager(
        ObjectNameFilters filters = ObjectNameFilters.All
    )
    {
        return new ReferencedItemsManager()
        {
            Filters = filters,
            Definition = CreateSimpleObjectProvider(),
            PseudoTables = new PseudoTableSet(
                activeSchema: s_testSchema,
                externalSources: [
                    new PseudoTable("mytable", new ObjectIdentifier("mytable", s_testSchema), ["col1", "col2", "col3"], PseudoTableType.Table)
                    {
                        SourceRef = new SourceRef(1, 0, 15)
                    }
                ]
            ),
            ActiveSchema = s_testSchema,
            FunctionNameProvider = new MsFunctionNameProvider(),
        };
    }
    private static SimpleObjectProvider CreateSimpleObjectProvider()
    {
        var provider = new SimpleObjectProvider(new Dictionary<ObjectHandle, ObjectType>
        {
            [ObjectHandle.Create([new Identifier("myproc")], s_testSchema, NameHandling.Lowercase)] = ObjectType.Procedure
        });
        return provider;
    }
}
