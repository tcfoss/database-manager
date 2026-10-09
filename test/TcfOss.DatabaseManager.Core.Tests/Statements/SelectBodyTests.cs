using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Tests.Statements;

public class SelectBodyTests
{
    [Theory]
    [InlineData("SELECT *")]
    [InlineData("SELECT t.* FROM t")]
    public void StrictPseudoTable_RejectsWildcards(string sql)
    {
        var query = ParseSelect(sql);

        Assert.Throws<DefinitionException.ViewSelectWildcard>(() => query.ToPseudoTable("view1", null, new SourceRef(1, 0, sql.Length), PseudoTableType.View));
    }

    [Fact]
    public void StrictPseudoTable_RejectsUnnamedExpressionAndDuplicateNames()
    {
        var unnamed = ParseSelect("SELECT 1");
        var duplicate = ParseSelect("SELECT col1, col1");

        Assert.Throws<DefinitionException.ViewSelectUnexpectedItem>(() => unnamed.ToPseudoTable("view1", null, null, PseudoTableType.View));
        Assert.Throws<DefinitionException.ViewNonUniqueSelectionName>(() => duplicate.ToPseudoTable("view1", null, null, PseudoTableType.View));
    }

    private static readonly string[] s_expectedTwoColumnOneAlias = ["col1", "col2", "alias1"];
    private static readonly string[] s_expectedCol1 = ["col1"];
    private static readonly string[] s_expectedCol2 = ["col2"];
    private static readonly string[] s_expectedFirstSecond = ["first", "second"];

    [Fact]
    public void RelaxedPseudoTable_InfersOnlyNamedColumns()
    {
        var query = ParseSelect("SELECT col1, t.col2, 1 AS alias1, 2, *");

        var actual = query.ToPseudoTableRelaxed("derived", null, null, PseudoTableType.DerivedTable);

        Assert.Equal(s_expectedTwoColumnOneAlias, actual.SelectableItems);
    }

    [Fact]
    public void SelectQueryWrapper_DelegatesFormattingReferencesAndPseudoTables()
    {
        var query = ParseSelect("SELECT col1");
        var wrapper = new SelectBody.SelectQuery(query);
        var context = new ReferencedItemsManager { Filters = ObjectNameFilters.All };
        var pseudoTables = new PseudoTableSet(null, []);

        wrapper.AddTablesToContext(pseudoTables);
        var reference = Assert.Single(wrapper.GetReferencedItems(context));
        Assert.Equal("col1", reference.Identifiers.Last().Name);
        Assert.Equal(s_expectedCol1, wrapper.ToPseudoTable("derived", null, null, PseudoTableType.DerivedTable).SelectableItems);
        Assert.Equal(s_expectedCol1, wrapper.ToPseudoTableRelaxed("derived", null, null, PseudoTableType.DerivedTable).SelectableItems);
        using var formatter = CreateFormatter();
        Assert.Equal("SELECT\n    \"col1\";", formatter.GetFormatted(new Select(wrapper)), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void SetOperation_StrictPseudoTable_FallsBackToRightSide()
    {
        var operation = new SelectBody.SetOperation(ParseSelect("SELECT 1").Body, SetOperator.Union, ParseSelect("SELECT 2 AS col2").Body);

        var actual = operation.ToPseudoTable("derived", null, null, PseudoTableType.DerivedTable);

        Assert.Equal(s_expectedCol2, actual.SelectableItems);
    }

    [Fact]
    public void ValuesBody_ReferencesRowsAndRejectsPseudoTables()
    {
        var body = new SelectBody.ValuesQuery(new Values(
        [
            [new SingleIdentifier(new Identifier("first"))],
            [new SingleIdentifier(new Identifier("second"))],
        ]));

        var items = body.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All }).ToList();

        Assert.Equal(s_expectedFirstSecond, items.Select(item => item.Identifiers.Last().Name));
        Assert.Throws<InvalidOperationException>(() => body.ToPseudoTable("derived", null, null, PseudoTableType.DerivedTable));
        Assert.Throws<InvalidOperationException>(() => body.ToPseudoTableRelaxed("derived", null, null, PseudoTableType.DerivedTable));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void SourceRef_CombinesIdentifierSpans(int count)
    {
        SqlValueList<Identifier> identifiers = [.. Enumerable.Range(0, count).Select(index => new Identifier($"name{index}") { Source = new SourceRef(7, index * 10, index * 10 + 5) })];

        var actual = SourceRef.FromIdentifiers(identifiers);

        if (count == 0)
        {
            Assert.Null(actual);
        }
        else
        {
            Assert.Equal(new SourceRef(7, 0, (count - 1) * 10 + 5), actual);
        }
    }

    private static Select ParseSelect(string sql)
    {
        return Assert.IsType<Select>(Assert.Single(new TextParser(new GenericLexer(), new Parser()).ParseText(sql)));
    }

    private static Formatter CreateFormatter()
    {
        var config = new ConfigBase
        {
            Catalog = new CatalogIdentifier("def"),
            ProjectDirectory = Path.GetTempPath(),
            QuoteStyle = QuoteStyle.Ansi,
            ValidationSettings = new ValidationSettings(),
            DatabaseAvailable = false,
        };
        return new Formatter(config, new TextParser(new GenericLexer(), new Parser()), new FunctionNameProvider());
    }
}
