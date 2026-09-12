using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.MySql.BuiltIn;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DatabaseComms;
using TcfOss.DatabaseManager.MySql.DatabaseObjects.Components;
using GenerationMode = TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes.GenerationMode;

namespace TcfOss.DatabaseManager.MySql.Tests.DatabaseComms;

public abstract class BaseInfoSchemaRepoTests
{
    protected abstract IRetrieveDatabaseObjects Repo { get; }
    protected abstract MyConfig MyConfig { get; }

    protected virtual string ExpectedGenerationExpression => "concat(concat_ws(': ',`title`,`subtitle`),if(`publication_year` is not null,concat(' (',`publication_year`,')'),''))";
    protected virtual ReferentialAction DefaultReferentialAction => ReferentialAction.NoAction;
    protected virtual string DefaultIntegerWidth => "";
    protected virtual RoutineParameterDirection? FunctionParameterDirection => null;

    [Fact]
    public async Task Get_Table_Identifiers_1()
    {
        var catalogSchema = new SchemaIdentifier("library_catalog", MyConfig.Catalog, MyConfig.QuoteStyle);
        var identifiers = (await Repo.GetTableIdentifiersAsync(catalogSchema, [], cancellationToken: TestContext.Current.CancellationToken)).ToList();
        Assert.NotEmpty(identifiers);
        Assert.Equal(7, identifiers.Count);
        Assert.Contains(identifiers, id => id.Name == "book");
        Assert.Contains(identifiers, id => id.Name == "contributor");
        Assert.Contains(identifiers, id => id.Name == "genre");
        Assert.Contains(identifiers, id => id.Name == "book_author");
        Assert.Contains(identifiers, id => id.Name == "book_genre");
        Assert.Contains(identifiers, id => id.Name == "book_search");
        Assert.Contains(identifiers, id => id.Name == "accidentally_added_table");
    }

    [Fact]
    public async Task Get_Table_Identifiers_1_One_Exclusion()
    {
        var catalogSchema = new SchemaIdentifier("library_catalog", MyConfig.Catalog, MyConfig.QuoteStyle);
        var identifiers = (await Repo.GetTableIdentifiersAsync(catalogSchema, ["book_search"], cancellationToken: TestContext.Current.CancellationToken)).ToList();
        Assert.Equal(6, identifiers.Count);
        Assert.DoesNotContain(identifiers, id => id.Name == "book_search");
        Assert.Contains(identifiers, id => id.Name == "book");
        Assert.Contains(identifiers, id => id.Name == "contributor");
        Assert.Contains(identifiers, id => id.Name == "genre");
        Assert.Contains(identifiers, id => id.Name == "book_author");
        Assert.Contains(identifiers, id => id.Name == "book_genre");
        Assert.Contains(identifiers, id => id.Name == "accidentally_added_table");
    }

    [Fact]
    public async Task Get_Table_Identifiers_1_Four_Exclusions()
    {
        var catalogSchema = new SchemaIdentifier("library_catalog", MyConfig.Catalog, MyConfig.QuoteStyle);
        var identifiers = (await Repo.GetTableIdentifiersAsync(catalogSchema, ["book_search", "book_genre", "contributor", "accidentally_added_table"], cancellationToken: TestContext.Current.CancellationToken)).ToList();
        Assert.Equal(3, identifiers.Count);
        Assert.DoesNotContain(identifiers, id => id.Name == "book_search");
        Assert.DoesNotContain(identifiers, id => id.Name == "book_genre");
        Assert.DoesNotContain(identifiers, id => id.Name == "contributor");
        Assert.Contains(identifiers, id => id.Name == "book");
        Assert.Contains(identifiers, id => id.Name == "genre");
        Assert.Contains(identifiers, id => id.Name == "book_author");
    }

    [Fact]
    public async Task Get_Table_Identifiers_2()
    {
        var catalogSchema = new SchemaIdentifier("library_identity", MyConfig.Catalog, MyConfig.QuoteStyle);
        var identifiers = (await Repo.GetTableIdentifiersAsync(catalogSchema, [], cancellationToken: TestContext.Current.CancellationToken)).ToList();
        Assert.Equal(3, identifiers.Count);
        Assert.Contains(identifiers, id => id.Name == "employee");
        Assert.Contains(identifiers, id => id.Name == "site_user");
        Assert.Contains(identifiers, id => id.Name == "patron");
    }

    [Fact]
    public async Task Get_Table_Identifiers_3()
    {
        var catalogSchema = new SchemaIdentifier("library_activity", MyConfig.Catalog, MyConfig.QuoteStyle);
        var identifiers = (await Repo.GetTableIdentifiersAsync(catalogSchema, [], cancellationToken: TestContext.Current.CancellationToken)).ToList();
        Assert.Equal(2, identifiers.Count);
        Assert.Contains(identifiers, id => id.Name == "active_rental");
        Assert.Contains(identifiers, id => id.Name == "rental_log");
    }

    [Fact]
    public async Task Table_Columns_General()
    {
        var tableId = new ObjectIdentifier("book", new SchemaIdentifier("library_catalog", MyConfig.Catalog, MyConfig.QuoteStyle), MyConfig.QuoteStyle);
        var table = await Repo.GetTableAsync(tableId, TestContext.Current.CancellationToken);
        Assert.NotNull(table);
        Assert.Equal("book", table.Name.Name);
        Assert.Equal(10, table.Columns.Count);
        Assert.Contains(table.Columns, c => c.Name.Name == "book_id");
        Assert.Contains(table.Columns, c => c.Name.Name == "title");
        Assert.Contains(table.Columns, c => c.Name.Name == "isbn");
        Assert.Contains(table.Columns, c => c.Name.Name == "publication_year");
        Assert.Contains(table.Columns, c => c.Name.Name == "publisher");
    }

    [Fact]
    public async Task Table_Column_Details_1()
    {
        var tableId = new ObjectIdentifier("book", new SchemaIdentifier("library_catalog", MyConfig.Catalog, MyConfig.QuoteStyle), MyConfig.QuoteStyle);
        var table = await Repo.GetTableAsync(tableId, TestContext.Current.CancellationToken);
        Assert.NotNull(table);

        var column = table.Columns.FirstOrDefault(c => c.Name.Name == "book_id");
        Assert.NotNull(column);
        Assert.Equal("book_id", column.Name.Name);

        var dataType = column.DataType as MyDataType.MyInt;
        Assert.NotNull(dataType);

        var nullability = column.Nullability;
        Assert.NotNull(nullability);
        Assert.IsType<ColumnOption.Nullability.NotNull>(nullability, exactMatch: true);

        Assert.True(column.AutoIncrement);
    }

    [Fact]
    public async Task Table_Column_Details_2()
    {
        var tableId = new ObjectIdentifier("book", new SchemaIdentifier("library_catalog", MyConfig.Catalog, MyConfig.QuoteStyle), MyConfig.QuoteStyle);
        var table = await Repo.GetTableAsync(tableId, TestContext.Current.CancellationToken);
        Assert.NotNull(table);

        var column = table.Columns.FirstOrDefault(c => c.Name.Name == "subtitle");
        Assert.NotNull(column);
        Assert.Equal("subtitle", column.Name.Name);

        var dataType = column.DataType as MyDataType.MyVarchar;
        Assert.NotNull(dataType);

        Assert.Equal(100u, dataType.Length);

        var nullability = column.Nullability;
        Assert.NotNull(nullability);
        Assert.IsType<ColumnOption.Nullability.Null>(nullability, exactMatch: true);
    }

    [Fact]
    public async Task Table_Column_Details_3()
    {
        var tableId = new ObjectIdentifier("book", new SchemaIdentifier("library_catalog", MyConfig.Catalog, MyConfig.QuoteStyle), MyConfig.QuoteStyle);
        var table = await Repo.GetTableAsync(tableId, TestContext.Current.CancellationToken);
        Assert.NotNull(table);

        var column = table.Columns.FirstOrDefault(c => c.Name.Name == "full_title");
        Assert.NotNull(column);
        Assert.Equal("full_title", column.Name.Name);

        var dataType = column.DataType as MyDataType.MyVarchar;
        Assert.NotNull(dataType);
        Assert.Equal(400u, dataType.Length);

        var nullability = column.Nullability;
        Assert.NotNull(nullability);
        Assert.IsType<ColumnOption.Nullability.Null>(nullability, exactMatch: true);

        var generation = column.Generated;
        Assert.NotNull(generation);

        var generationAs = column.Generated as ColumnOption.Generated.AsExpression;
        Assert.NotNull(generationAs);
        Assert.Equal(ExpectedGenerationExpression, generationAs.Expression.ToSql(), ignoreCase: true, ignoreAllWhiteSpace: true);
        Assert.Equal(GenerationMode.Virtual, generationAs.Mode);
    }

    [Fact]
    public async Task Table_Primary_Key()
    {
        var tableId = new ObjectIdentifier("book", new SchemaIdentifier("library_catalog", MyConfig.Catalog, MyConfig.QuoteStyle), MyConfig.QuoteStyle);
        var table = await Repo.GetTableAsync(tableId, TestContext.Current.CancellationToken);
        Assert.NotNull(table);

        var primaryKey = table.PrimaryKey;
        Assert.NotNull(primaryKey);

        var columns = primaryKey.Columns;
        Assert.Single(columns);
        var column = columns.FirstOrDefault();
        Assert.NotNull(column);
        var columnAsKeyPart = column as KeyPart.Column;
        Assert.NotNull(columnAsKeyPart);
        Assert.Equal("book_id", columnAsKeyPart.Name.Name);
    }

    [Fact]
    public async Task Table_Indexes_1()
    {
        var tableId = new ObjectIdentifier("book_author", new SchemaIdentifier("library_catalog", MyConfig.Catalog, MyConfig.QuoteStyle), MyConfig.QuoteStyle);
        var table = await Repo.GetTableAsync(tableId, TestContext.Current.CancellationToken);
        Assert.NotNull(table);

        var primaryKey = table.PrimaryKey;
        Assert.NotNull(primaryKey);

        var primaryKeyColumns = primaryKey.Columns;
        Assert.Equal(2, primaryKeyColumns.Count);

        var indexes = table.Keys;
        Assert.NotNull(indexes);
        Assert.Single(indexes);

        var index = indexes.Values.FirstOrDefault();
        Assert.NotNull(index);

        var indexAsStandard = index as MyKey.Standard;
        Assert.NotNull(indexAsStandard);
        Assert.NotNull(indexAsStandard.Name);
        Assert.Equal("idx_book_author_contributor_id", indexAsStandard.Name.Name);

        var columns = indexAsStandard.Columns;
        Assert.NotNull(columns);
        Assert.Single(columns);

        var column = columns.FirstOrDefault() as KeyPart.Column;
        Assert.NotNull(column);
        Assert.Equal("contributor_id", column.Name.Name);

        Assert.Equal(Direction.Ascending, column.Direction);
        Assert.Null(column.Length);
        Assert.Equal(IndexMethod.Btree, indexAsStandard.IndexMethod);
    }

    [Fact]
    public async Task Foreign_Keys()
    {
        var tableId = new ObjectIdentifier("book_author", new SchemaIdentifier("library_catalog", MyConfig.Catalog, MyConfig.QuoteStyle), MyConfig.QuoteStyle);
        var table = await Repo.GetTableAsync(tableId, TestContext.Current.CancellationToken);
        Assert.NotNull(table);

        var foreignKeys = table.ForeignKeys;
        Assert.NotNull(foreignKeys);
        Assert.Equal(2, foreignKeys.Count);

        var first = foreignKeys.FirstOrDefault(x => x.Key.Name == "fk_book_author_book").Value;
        Assert.NotNull(first);
        Assert.NotNull(first.Name);
        Assert.Equal("fk_book_author_book", first.Name.Name);
        Assert.Single(first.Columns);

        var columns = first.Columns;
        Assert.Single(columns);

        var column = columns.FirstOrDefault();
        Assert.NotNull(column);
        Assert.Equal("book_id", column.Name);

        var referencedTable = first.ReferencedTable;
        Assert.NotNull(referencedTable);
        Assert.Equal("book", referencedTable.Name);

        var referencedColumns = first.ReferencedColumns;
        Assert.Single(referencedColumns);

        var referencedColumn = referencedColumns.FirstOrDefault();
        Assert.NotNull(referencedColumn);
        Assert.Equal("book_id", referencedColumn.Name);

        Assert.Equal(ReferentialAction.Restrict, first.OnDelete);
        Assert.Equal(ReferentialAction.Cascade, first.OnUpdate);
    }

    [Fact]
    public async Task Multiple_Keys_Cross_Schema()
    {
        var tableId = new ObjectIdentifier("active_rental", new SchemaIdentifier("library_activity", MyConfig.Catalog, MyConfig.QuoteStyle), MyConfig.QuoteStyle);
        var table = await Repo.GetTableAsync(tableId, TestContext.Current.CancellationToken);
        Assert.NotNull(table);

        var primaryKey = table.PrimaryKey;
        Assert.NotNull(primaryKey);

        var primaryKeyColumns = primaryKey.Columns;
        Assert.Single(primaryKeyColumns);
        var primaryKeyColumn = primaryKeyColumns.FirstOrDefault();
        Assert.NotNull(primaryKeyColumn);
        var primaryKeyColumnAsKeyPart = primaryKeyColumn as KeyPart.Column;
        Assert.NotNull(primaryKeyColumnAsKeyPart);
        Assert.Equal("rental_id", primaryKeyColumnAsKeyPart.Name.Name);


        Assert.NotNull(table.UniqueKeys);
        Assert.Single(table.UniqueKeys);
        var uniqueKey = table.UniqueKeys.FirstOrDefault().Value;
        Assert.NotNull(uniqueKey);
        Assert.NotNull(uniqueKey.Name);
        Assert.Equal("uc_active_rental_book_id", uniqueKey.Name.Name);

        Assert.Equal(2, uniqueKey.Columns.Count);
        var uniqueKeyColumns = uniqueKey.Columns.ToList();

        var uniqueCol1 = uniqueKeyColumns[0] as KeyPart.Column;
        Assert.NotNull(uniqueCol1);
        Assert.Equal("book_id", uniqueCol1.Name.Name);

        var uniqueCol2 = uniqueKeyColumns[1] as KeyPart.Column;
        Assert.NotNull(uniqueCol2);
        Assert.Equal("really_active", uniqueCol2.Name.Name);

        Assert.NotNull(uniqueKey.IndexMethod);
        Assert.Equal(IndexMethod.Btree, uniqueKey.IndexMethod);


        var foreignKeys = table.ForeignKeys;
        Assert.NotNull(foreignKeys);
        Assert.Equal(2, foreignKeys.Count);

        var first = foreignKeys.FirstOrDefault(x => x.Key.Name == "fk_active_rental_book").Value;
        Assert.NotNull(first);
        Assert.NotNull(first.Name);
        Assert.Equal("fk_active_rental_book", first.Name.Name);
        Assert.Single(first.Columns);

        var referencedTable = first.ReferencedTable;
        Assert.NotNull(referencedTable);
        Assert.Equal("book", referencedTable.Name);
        Assert.Equal("library_catalog", referencedTable.Schema.Name);

        var referencedColumns = first.ReferencedColumns;
        Assert.Single(referencedColumns);

        var foreignColumn = referencedColumns.FirstOrDefault();
        Assert.NotNull(foreignColumn);
        Assert.Equal("book_id", foreignColumn.Name);

        Assert.Equal(ReferentialAction.Restrict, first.OnDelete);
        Assert.Equal(DefaultReferentialAction, first.OnUpdate);
    }

    [Fact]
    public async Task Function()
    {
        var functionId = new ObjectIdentifier("get_late_charge", new SchemaIdentifier("library_activity", MyConfig.Catalog, MyConfig.QuoteStyle), MyConfig.QuoteStyle);
        var function = await Repo.GetFunctionAsync(functionId, TestContext.Current.CancellationToken);
        Assert.NotNull(function);
        Assert.Equal("get_late_charge", function.Name.Name);

        Assert.Equal(SecurityContext.Definer, function.SecurityContext);
        Assert.Equal(new Account.IdentityWithHost(new ExtendedIdentifier("admin", ExtendedQuoteStyle.Backticks), new ExtendedIdentifier("localhost", ExtendedQuoteStyle.Backticks)), function.Definer.Account);

        Assert.Equal("DECIMAL(10,2)", function.ReturnType.ToSql());

        var parameters = function.Parameters.ToList();
        Assert.Equal(2, parameters.Count);
        Assert.Equal("rental_id", parameters[0].Name.Name);
        Assert.Equal($"INT{DefaultIntegerWidth} SIGNED", parameters[0].DataType.ToSql());
        Assert.Equal(FunctionParameterDirection, Assert.IsType<RoutineParameter.Directed>(parameters[0]).Direction);
        Assert.Equal("lateness_rate", parameters[1].Name.Name);
        Assert.Equal("DECIMAL(10,2) SIGNED", parameters[1].DataType.ToSql());
        Assert.Equal(FunctionParameterDirection, Assert.IsType<RoutineParameter.Directed>(parameters[1]).Direction);

        var actual = function.Body.ToSql();
        var expected = "BEGIN DECLARE late_charge DECIMAL(10,2) DEFAULT 0.00; IF CURRENT_DATE() <= (SELECT due_date FROM active_rental AS ar WHERE ar.rental_id = rental_id) THEN RETURN 0.00; END IF; SELECT (DATEDIFF(CURRENT_DATE(), ar.due_date) * lateness_rate) INTO late_charge FROM active_rental AS ar WHERE ar.rental_id = rental_id; RETURN IFNULL(late_charge, 0); END";
        Assert.Equal(expected, actual, ignoreCase: true, ignoreAllWhiteSpace: true);
    }

    [Fact]
    public async Task Get_Event_Identifiers()
    {
        var activitySchema = new SchemaIdentifier("library_identity", MyConfig.Catalog, MyConfig.QuoteStyle);
        var identifiers = (await Repo.GetEventIdentifiersAsync(activitySchema, [], TestContext.Current.CancellationToken)).ToList();
        Assert.Single(identifiers);
        Assert.Contains(identifiers, id => id.Name == "deactivate_stale_users");
    }

    [Fact]
    public async Task Event()
    {
        var eventId = new ObjectIdentifier("deactivate_stale_users", new SchemaIdentifier("library_identity", MyConfig.Catalog, MyConfig.QuoteStyle), MyConfig.QuoteStyle);
        var evt = await Repo.GetEventAsync(eventId, TestContext.Current.CancellationToken);
        Assert.NotNull(evt);
        Assert.Equal("deactivate_stale_users", evt.Name.Name);

        Assert.Equal(new Account.IdentityWithHost(new ExtendedIdentifier("admin", ExtendedQuoteStyle.Backticks), new ExtendedIdentifier("localhost", ExtendedQuoteStyle.Backticks)), evt.Definer.Account);

        var schedule = evt.Schedule as EventSchedule.Every;
        Assert.NotNull(schedule);
        Assert.Equal(new LiteralValue(new Value.Number("1", false)), schedule.Quantity as LiteralValue);
        Assert.Equal(DateTimeUnit.Day, schedule.Unit);

        Assert.Equal(new LiteralValue(new Value.SingleQuotedString("2024-01-01 00:00:00")), schedule.Start as LiteralValue);
        Assert.Null(schedule.End);

        var actual = evt.Body.ToSql();
        var expected = "BEGIN UPDATE site_user SET is_active = FALSE WHERE is_active = TRUE AND last_login < NOW() - INTERVAL 1 YEAR; END";
        Assert.Equal(expected, actual, ignoreCase: true, ignoreAllWhiteSpace: true);
    }
}
