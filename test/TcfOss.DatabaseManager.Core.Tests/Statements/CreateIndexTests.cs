using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Tests.Statements;

public class CreateIndexTests
{
    private static readonly ObjectName s_table = new([new Identifier("mytable")]);
    private static readonly Identifier s_name = new("ix");
    private static readonly SqlValueList<KeyPart> s_columns = [new KeyPart.Column(new Identifier("col1"))];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Unique_ToConstraint_PreservesOptions(bool options)
    {
        var index = CreateUnique(options);

        var actual = index.ToStatementConstraint();

        Assert.Equal(new StatementTableConstraint.UniqueConstraint(s_columns, s_name)
        {
            KeyLabel = KeyLabel.Key,
            IndexMethod = index.IndexMethod,
            IndexOrganization = index.IndexOrganization,
            IncludedColumns = index.IncludedColumns,
            Options = index.Options,
            StorageLocation = index.StorageLocation,
            Comment = index.Comment,
        }, actual);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Unique_ToIndex_PreservesFilterAndOptions(bool filtered)
    {
        var index = CreateUnique(true) with { Filter = filtered ? new SingleIdentifier(new Identifier("predicate")) : null };

        var actual = index.ToStatementIndex();

        Assert.Equal(new StatementTableConstraint.UniqueIndex(s_columns, s_name)
        {
            KeyLabel = KeyLabel.Key,
            IndexMethod = index.IndexMethod,
            IndexOrganization = index.IndexOrganization,
            IncludedColumns = index.IncludedColumns,
            Options = index.Options,
            StorageLocation = index.StorageLocation,
            Comment = index.Comment,
            Filter = index.Filter,
        }, actual);
    }

    [Fact]
    public void Unique_FilteredIndex_CannotBecomeConstraint()
    {
        var index = CreateUnique(false) with { Filter = new SingleIdentifier(new Identifier("predicate")) };

        Assert.Throws<InvalidOperationException>(index.ToStatementConstraint);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void SpecializedIndexes_ConvertAndSerialize(bool spatial, bool withComment)
    {
        Comment? comment = withComment ? new Comment("description") : null;
        StatementTableConstraint actual;
        string sql;
        if (spatial)
        {
            var index = new CreateIndex.Spatial(s_columns, s_name, s_table) { Comment = comment };
            actual = index.ToStatementConstraint();
            sql = index.ToSql();
            Assert.Equal(new StatementTableConstraint.Spatial(s_columns, s_name) { Comment = comment, KeyLabel = KeyLabel.Key }, actual);
        }
        else
        {
            var index = new CreateIndex.FullText(s_columns, s_name, s_table) { Comment = comment };
            actual = index.ToStatementConstraint();
            sql = index.ToSql();
            Assert.Equal(new StatementTableConstraint.FullText(s_columns, s_name) { Comment = comment, KeyLabel = KeyLabel.Key }, actual);
        }
        string kind = spatial ? "SPATIAL" : "FULLTEXT";
        string suffix = withComment ? " COMMENT 'description'" : "";
        Assert.Equal($"CREATE {kind} INDEX ix ON mytable (col1){suffix}", sql);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetReferencedItems_RespectsFilters(bool includeTable)
    {
        var context = new ReferencedItemsManager
        {
            ActiveSchema = new SchemaIdentifier("schema", new CatalogIdentifier("def")),
            Filters = includeTable ? ObjectNameFilters.All : ObjectNameFilters.None,
        };

        var items = CreateUnique(false).GetReferencedItems(context).ToList();

        if (includeTable)
        {
            var item = Assert.Single(items);
            Assert.Equal("mytable", item.Identifiers.Last().Name);
        }
        else
        {
            Assert.Empty(items);
        }
    }

    private static CreateIndex.Unique CreateUnique(bool options)
    {
        return new CreateIndex.Unique(s_columns, s_name, s_table)
        {
            IndexMethod = options ? IndexMethod.Btree : null,
            IndexOrganization = options ? IndexOrganization.Nonclustered : null,
            IncludedColumns = options ? new IncludedColumns([new Identifier("col2")]) : null,
            Options = options ? [new StatementIndexOption.FillFactor(80)] : null,
            StorageLocation = options ? new IndexStorageLocation.Filegroup(new Identifier("PRIMARY")) : null,
            Comment = options ? new Comment("description") : null,
        };
    }
}
