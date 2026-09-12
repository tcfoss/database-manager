using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;

namespace TcfOss.DatabaseManager.Core.Tests.DefinitionBuilding;

public class PseudoTableSetTests
{
    private readonly PseudoTableSet _pseudoTableSet;
    public PseudoTableSetTests()
    {
        var catalogId = new CatalogIdentifier("def", QuoteStyle.Ansi);
        var otherCatalogId = new CatalogIdentifier("other", QuoteStyle.Ansi);
        var schemaId1 = new SchemaIdentifier("schema1", catalogId, QuoteStyle.Ansi);
        var schemaId2 = new SchemaIdentifier("schema2", catalogId, QuoteStyle.Ansi);

        var externalSources = new PseudoTable[]
        {
            new("table1", new ObjectIdentifier("table1", schemaId1, QuoteStyle.Ansi), ["col1", "col2"], PseudoTableType.Table),
            new("table2", new ObjectIdentifier("table2", schemaId1, QuoteStyle.Ansi), ["col1", "col2", "col3"], PseudoTableType.Table),
            new("table3", new ObjectIdentifier("table3", schemaId1, QuoteStyle.Ansi), ["col1", "col2"], PseudoTableType.Table),
            new("table4", new ObjectIdentifier("table4", schemaId1, QuoteStyle.Ansi), ["col1", "col2"], PseudoTableType.Table),
            new("table1", new ObjectIdentifier("table1", schemaId2, QuoteStyle.Ansi), ["col1", "col2", "col3", "col4"], PseudoTableType.Table),
            new("table1", new ObjectIdentifier("table1", new SchemaIdentifier("schema1", otherCatalogId, QuoteStyle.Ansi), QuoteStyle.Ansi), ["colX", "colY"], PseudoTableType.Table),
            new("table3", new ObjectIdentifier("table3", schemaId2, QuoteStyle.Ansi), ["col1", "col2", "col3"], PseudoTableType.Table),
        };

        var localSources = new PseudoTable[]
        {
            new("local1", new ObjectIdentifier("table1", schemaId1, QuoteStyle.Ansi), ["col1", "col2"], PseudoTableType.Table),
            new("local2", new ObjectIdentifier("table3", schemaId2, QuoteStyle.Ansi), ["col1", "col2", "col3"], PseudoTableType.Table),
            new("local3", new ObjectIdentifier("cte", new SchemaIdentifier("", new CatalogIdentifier(""))), ["colA", "colB"], PseudoTableType.CommonTableExpression),
        };

        _pseudoTableSet = new PseudoTableSet(schemaId1, externalSources, [localSources]);
    }

    [Theory]
    [InlineData(new[] { "table2" }, "table2", "schema1", "def")]
    [InlineData(new[] { "schema2", "table3" }, "table3", "schema2", "def")]
    [InlineData(new[] { "def", "schema1", "table1" }, "table1", "schema1", "def")]
    [InlineData(new[] { "schema2", "table1" }, "table1", "schema2", "def")]
    public void GetRequiredPseudoTable_FindsTable(string[] componentNames, string expectedTableName, string expectedSchemaName, string expectedCatalogName)
    {
        var objectName = new ObjectName([.. componentNames.Select(name => new Identifier(name))]);
        var table = _pseudoTableSet.GetRequiredPseudoTable(objectName);

        Assert.Equal(expectedTableName, table.Identifier!.Name);
        Assert.Equal(expectedSchemaName, table.Identifier!.Schema.Name);
        Assert.Equal(expectedCatalogName, table.Identifier!.Schema.Catalog.Name);
    }

    [Theory]
    [InlineData([new string[0]])]
    [InlineData([new[] { "a", "b", "c", "d" }])]
    public void GetRequiredPseudoTable_InvalidLength_Throws(string[] componentNames)
    {
        var objectName = new ObjectName([.. componentNames.Select(name => new Identifier(name))]);
        Assert.Throws<IdentifierMismatchException.IdentifierLengthException>(() => _pseudoTableSet.GetRequiredPseudoTable(objectName));
    }

    [Theory]
    [InlineData([new[] { "table5" }])]
    [InlineData([new[] { "schema2", "table4" }])]
    [InlineData([new[] { "other", "schema2", "table1" }])]

    public void GetRequiredPseudoTable_TableNotFound_Throws(string[] componentNames)
    {
        var objectName = new ObjectName([.. componentNames.Select(name => new Identifier(name))]);
        Assert.Throws<SqlSyntaxException.SelectSourceNotFound>(() => _pseudoTableSet.GetRequiredPseudoTable(objectName));
    }

    [Theory]
    [InlineData([new[] { "table1" }])]
    [InlineData([new[] { "schema1", "table1" }])]
    public void GetRequiredPseudoTable_NonUniqueTable_Throws(string[] componentNames)
    {
        var objectName = new ObjectName([.. componentNames.Select(name => new Identifier(name))]);
        Assert.Throws<SqlSyntaxException.ViewNonUniqueReferenceName>(() => _pseudoTableSet.GetRequiredPseudoTable(objectName));
    }

    [Theory]
    [InlineData(new[] { "table1" }, 3)]
    [InlineData(new[] { "table3" }, 2)]
    [InlineData(new[] { "table2" }, 1)]
    [InlineData(new[] { "schema1", "table1" }, 2)]
    [InlineData(new[] { "def", "schema1", "table1" }, 1)]
    public void GetPseudoTables_ReturnsAllMatches(string[] componentNames, int expectedCount)
    {
        var objectName = new ObjectName([.. componentNames.Select(name => new Identifier(name))]);
        var tables = _pseudoTableSet.GetPseudoTables(objectName);
        Assert.Equal(expectedCount, tables.Length);

        var compoundIdentifier = new CompoundIdentifier([.. componentNames.Select(name => new Identifier(name))]);
        var tablesFromCompound = _pseudoTableSet.GetPseudoTables(compoundIdentifier);
        Assert.Equal(expectedCount, tablesFromCompound.Length);
    }


    [Theory]
    [InlineData(new[] { "local1", "col1" }, "col1", "table1", "schema1")]
    [InlineData(new[] { "local2", "col2" }, "col2", "table3", "schema2")]
    [InlineData(new[] { "colA" }, "colA", "cte", "")]
    [InlineData(new[] { "col3" }, "col3", "table3", "schema2")]
    public void GetRequiredSelectableElement_FindsColumn(string[] componentNames, string expectedName, string expectedSource, string? expectedSchema)
    {
        var objectName = new CompoundIdentifier([.. componentNames.Select(name => new Identifier(name))]);
        var element = _pseudoTableSet.GetRequiredSelectableElement(objectName);

        Assert.Equal(expectedName, element.Name);
        Assert.Equal(expectedSource, element.Source);
        Assert.Equal(expectedSchema, element.Schema);
    }

    [Fact]
    public void GetRequiredSelectableElement_ColumnNotFound_Throws()
    {
        var objectName = new CompoundIdentifier([new Identifier("local1"), new Identifier("colX")]);
        Assert.Throws<SqlSyntaxException.SelectIdentifierNotFound>(() => _pseudoTableSet.GetRequiredSelectableElement(objectName));
    }

    [Theory]
    [InlineData([new[] { "col1" }])]
    [InlineData([new[] { "col2" }])]
    public void GetRequiredSelectableElement_NotUnique_Throws(string[] componentNames)
    {
        var objectName = new CompoundIdentifier([.. componentNames.Select(name => new Identifier(name))]);
        Assert.Throws<SqlSyntaxException.SelectIdentifierNotUnique>(() => _pseudoTableSet.GetRequiredSelectableElement(objectName));
    }

    [Fact]
    public void GetRequiredSelectableElement_InvalidLength_Throws()
    {
        var objectName = new CompoundIdentifier([new Identifier("grandparent"), new Identifier("parent"), new Identifier("object"), new Identifier("column")]);
        Assert.Throws<IdentifierMismatchException.IdentifierLengthException>(() => _pseudoTableSet.GetRequiredSelectableElement(objectName));

        objectName = new CompoundIdentifier([]);
        Assert.Throws<IdentifierMismatchException.IdentifierLengthException>(() => _pseudoTableSet.GetRequiredSelectableElement(objectName));
    }

    [Fact]
    public void LeaveSelectScope_NoScope_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            _pseudoTableSet.LeaveSelectScope();
            _pseudoTableSet.LeaveSelectScope();
        });
    }
}
