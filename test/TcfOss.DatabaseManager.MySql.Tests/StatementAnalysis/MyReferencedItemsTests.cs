using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Tests;
using TcfOss.DatabaseManager.MySql.BuiltIn;
using TcfOss.DatabaseManager.MySql.Lexing;
using TcfOss.DatabaseManager.MySql.Parsing;

namespace TcfOss.DatabaseManager.MySql.Tests.StatementAnalysis;

public class MyReferencedItemsTests
{
    [Theory]
    [InlineData("CREATE PROCEDURE proc() SELECT col1 FROM mytable", "mytable", "col1")]
    [InlineData("CREATE FUNCTION func() RETURNS INT RETURN (SELECT col1 FROM mytable)", "mytable", "col1")]
    [InlineData("CREATE EVENT ev ON SCHEDULE EVERY 1 DAY DO SELECT col1 FROM mytable", "mytable", "col1")]
    [InlineData("CREATE TRIGGER trg BEFORE INSERT ON mytable FOR EACH ROW SELECT col1 FROM mytable", "mytable", "mytable", "col1")]
    [InlineData("LOOP SELECT col1 FROM mytable; END LOOP", "mytable", "col1")]
    [InlineData("label1: LOOP SELECT col1 FROM mytable; END LOOP label1", "mytable", "col1")]
    [InlineData("DECLARE cur CURSOR FOR SELECT col1 FROM mytable", "mytable", "col1")]
    [InlineData("DECLARE n INT DEFAULT (SELECT col1 FROM mytable)", "mytable", "col1")]
    [InlineData("DECLARE CONTINUE HANDLER FOR SQLSTATE '45000' SELECT col1 FROM mytable", "mytable", "col1")]
    [InlineData("CALL myproc((SELECT col1 FROM mytable))", "myproc", "mytable", "col1")]
    public void Statement_TraversesRoutineAndCursorBodies(string sql, params string[] expectedNames)
    {
        var parser = new TextParser(new MyLexer(), new MyParser());
        var statement = Assert.Single(parser.ParseText(sql));

        var items = statement.GetReferencedItems(CreateManager()).ToList();

        items.AssertEqualItemNames(expectedNames);
    }

    [Theory]
    [InlineData("USE schema1")]
    [InlineData("SHOW WARNINGS")]
    [InlineData("SHOW COUNT(*) WARNINGS")]
    [InlineData("SHOW ERRORS")]
    [InlineData("PREPARE stmt FROM 'SELECT 1'")]
    [InlineData("PREPARE stmt FROM @query")]
    [InlineData("EXECUTE stmt")]
    [InlineData("DEALLOCATE PREPARE stmt")]
    [InlineData("DECLARE cond CONDITION FOR SQLSTATE '45000'")]
    [InlineData("OPEN cur")]
    [InlineData("CLOSE cur")]
    [InlineData("FETCH cur INTO n")]
    [InlineData("FETCH GROUP NEXT ROW")]
    [InlineData("LEAVE label1")]
    [InlineData("ITERATE label1")]
    [InlineData("RETURN 1")]
    public void Statement_WithNoReferences_ReturnsEmpty(string sql)
    {
        var parser = new TextParser(new MyLexer(), new MyParser());
        var statement = Assert.Single(parser.ParseText(sql));

        Assert.Empty(statement.GetReferencedItems(CreateManager()));
    }

    private static ReferencedItemsManager CreateManager()
    {
        var schema = new SchemaIdentifier("schema1", new CatalogIdentifier("def"));
        return new ReferencedItemsManager
        {
            Filters = ObjectNameFilters.All,
            ActiveSchema = schema,
            FunctionNameProvider = new MyFunctionNameProvider(),
            Definition = new SimpleObjectProvider(new Dictionary<ObjectHandle, ObjectType>
            {
                [ObjectHandle.Create([new Identifier("myproc")], schema, NameHandling.None)] = ObjectType.Procedure,
            }),
            PseudoTables = new PseudoTableSet(schema,
            [
                new PseudoTable("mytable", new ObjectIdentifier("mytable", schema), ["col1", "col2"], PseudoTableType.Table),
            ]),
        };
    }

    [Fact]
    public void MatchAgainstExpression()
    {
        var text = "MATCH(a, b, c) AGAINST(d IN NATURAL LANGUAGE MODE)";
        var expr = ParseExpression(text);

        List<ItemRef> items = [.. expr.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.Unknown })];

        Assert.Equal(4, items.Count);
        Assert.All(items, i => Assert.Equal(ItemType.Unknown, i.Type));

        items.AssertEqualItemNames("a", "b", "c", "d");
    }

    private static Expression ParseExpression(string sql)
    {
        var tokens = new MyLexer().Tokenize(sql);
        var state = new ParserState([.. tokens]);
        return new MyParser().ExpressionParser.ParseExpr(state);
    }
}
