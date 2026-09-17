using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseComms;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.MySql.BuiltIn;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DatabaseComms.EntityFramework;
using TcfOss.DatabaseManager.MySql.DatabaseComms.EntityFramework.Entities;
using TcfOss.DatabaseManager.MySql.DatabaseComms.InfoSchemaHelperModels;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;
using TcfOss.DatabaseManager.MySql.DatabaseObjects.Components;
using TcfOss.DatabaseManager.MySql.Lexing;
using TcfOss.DatabaseManager.MySql.Parsing;

namespace TcfOss.DatabaseManager.MySql.DatabaseComms;

public partial class InfoSchemaRepo : IRetrieveDatabaseObjects, IDisposable
{
    private readonly SemaphoreSlim _queryGate = new(10);
    private readonly ConcurrentDictionary<(string Catalog, string Schema), Lazy<Task<List<TriggerDto>>>> _triggersBySchema = [];
    private readonly ConcurrentDictionary<(string Catalog, string Schema), Lazy<Task<SchemaTableData>>> _tableDataBySchema = [];
    private readonly ConcurrentDictionary<(string Catalog, string Schema), Lazy<Task<SchemaIndexData>>> _indexDataBySchema = [];
    private readonly ConcurrentDictionary<(string Catalog, string Schema), Lazy<Task<SchemaForeignKeyData>>> _foreignKeyDataBySchema = [];
    private readonly ConcurrentDictionary<(string Catalog, string Schema), Lazy<Task<SchemaRoutineData>>> _routineDataBySchema = [];
    private readonly ConcurrentDictionary<(string Catalog, string Schema), Lazy<Task<IReadOnlyDictionary<string, ViewEntity>>>> _viewsBySchema = [];
    private readonly ConcurrentDictionary<(string Catalog, string Schema), Lazy<Task<IReadOnlyDictionary<string, EventEntity>>>> _eventsBySchema = [];

    private readonly QuoteStyle _quoteStyle;
    private readonly MyConfig _config;
    private readonly MyLexer _lexer;
    private readonly MyParser _parser;
    private readonly InfoSchemaRepoHelper _helper;

    private readonly IDbContextFactory<InfoSchemaContext> _contextFactory;
    private readonly IRetrieveRawEntities _rawEntityRetriever;
    private static readonly Regex s_algorithmRegex = GetAlgorithmRegex();
    private static readonly Regex s_checkOptionRegex = GetCheckOptionRegex();


    public InfoSchemaRepo(MyConfig config, IDbContextFactory<InfoSchemaContext> contextFactory, IRetrieveRawEntities rawEntityRetriever)
    {
        _contextFactory = contextFactory;
        _rawEntityRetriever = rawEntityRetriever;
        _config = config;
        _quoteStyle = config.QuoteStyle;
        _lexer = new MyLexer();
        _parser = new MyParser();
        _helper = new InfoSchemaRepoHelper(_lexer, _parser, config);
    }

    public async Task<List<ObjectIdentifier>> GetTableIdentifiersAsync(SchemaIdentifier schemaId, IEnumerable<string> excludedNames, bool refreshCache = true, CancellationToken cancellationToken = default)
    {
        if (refreshCache)
        {
            // Refresh schema-local caches for each schema load.
            _ = _triggersBySchema.TryRemove((schemaId.Catalog.Name, schemaId.Name), out _);
            _ = _tableDataBySchema.TryRemove((schemaId.Catalog.Name, schemaId.Name), out _);
            _ = _indexDataBySchema.TryRemove((schemaId.Catalog.Name, schemaId.Name), out _);
            _ = _foreignKeyDataBySchema.TryRemove((schemaId.Catalog.Name, schemaId.Name), out _);
            _ = _routineDataBySchema.TryRemove((schemaId.Catalog.Name, schemaId.Name), out _);
            _ = _viewsBySchema.TryRemove((schemaId.Catalog.Name, schemaId.Name), out _);
            _ = _eventsBySchema.TryRemove((schemaId.Catalog.Name, schemaId.Name), out _);
        }

        await using QueryLease<InfoSchemaContext> lease = await GetConnectionAsync(cancellationToken);
        InfoSchemaContext context = lease.Context;

        IQueryable<ObjectIdentifier> query = from t in context.Tables
                                             where t.TableCatalog == schemaId.Catalog.Name
                                             && t.TableSchema == schemaId.Name
                                             && t.TableType == "BASE TABLE"
                                             && !excludedNames.Contains(t.TableName)
                                             && t.TableName != StoredMetadataConstants.TableName
                                             select new ObjectIdentifier(t.TableName, schemaId, _quoteStyle);
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<List<ObjectIdentifier>> GetProcedureIdentifiersAsync(SchemaIdentifier schemaId, IEnumerable<string> excludedNames, CancellationToken cancellationToken = default)
    {
        await using QueryLease<InfoSchemaContext> lease = await GetConnectionAsync(cancellationToken);
        InfoSchemaContext context = lease.Context;

        IQueryable<ObjectIdentifier> query = from r in context.Routines
                                             where r.RoutineCatalog == schemaId.Catalog.Name
                                             && r.RoutineSchema == schemaId.Name
                                             && r.RoutineType == "PROCEDURE"
                                             && !excludedNames.Contains(r.RoutineName)
                                             select new ObjectIdentifier(r.RoutineName, schemaId, _quoteStyle);
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<List<ObjectIdentifier>> GetFunctionIdentifiersAsync(SchemaIdentifier schemaId, IEnumerable<string> excludedNames, CancellationToken cancellationToken = default)
    {
        await using QueryLease<InfoSchemaContext> lease = await GetConnectionAsync(cancellationToken);
        InfoSchemaContext context = lease.Context;

        IQueryable<ObjectIdentifier> query = from r in context.Routines
                                             where r.RoutineCatalog == schemaId.Catalog.Name
                                             && r.RoutineSchema == schemaId.Name
                                             && r.RoutineType == "FUNCTION"
                                             && !excludedNames.Contains(r.RoutineName)
                                             select new ObjectIdentifier(r.RoutineName, schemaId, _quoteStyle);
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<List<ObjectIdentifier>> GetViewIdentifiersAsync(SchemaIdentifier schemaId, IEnumerable<string> excludedNames, CancellationToken cancellationToken = default)
    {
        await using QueryLease<InfoSchemaContext> lease = await GetConnectionAsync(cancellationToken);
        InfoSchemaContext context = lease.Context;

        IQueryable<ObjectIdentifier> query = from v in context.Views
                                             where v.TableCatalog == schemaId.Catalog.Name
                                             && v.TableSchema == schemaId.Name
                                             && !excludedNames.Contains(v.TableName)
                                             select new ObjectIdentifier(v.TableName, schemaId, _quoteStyle);
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<List<ObjectIdentifier>> GetEventIdentifiersAsync(SchemaIdentifier schemaId, IEnumerable<string> excludedNames, CancellationToken cancellationToken = default)
    {
        await using QueryLease<InfoSchemaContext> lease = await GetConnectionAsync(cancellationToken);
        InfoSchemaContext context = lease.Context;

        IQueryable<ObjectIdentifier> query = from e in context.Events
                                             where e.EventCatalog == schemaId.Catalog.Name
                                             && e.EventSchema == schemaId.Name
                                             && !excludedNames.Contains(e.EventName)
                                             select new ObjectIdentifier(e.EventName, schemaId, _quoteStyle);
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<MyTable> GetTableAsync(ObjectIdentifier tableId, CancellationToken cancellationToken = default)
    {
        (string catalog, string schema, string name) = tableId.Strings;

        SchemaTableData tableData = await GetSchemaTableDataAsync(catalog, schema);
        if (!tableData.TableInfoByName.TryGetValue(name, out TableDto isTable))
        {
            throw new InvalidOperationException($"Failed to load metadata for table '{tableId}'.");
        }

        SqlValueList<MyColumn> columns = GetColumns(tableData.GetColumnRows(name), tableId);
        Comment? tableComment = null;
        if (!string.IsNullOrWhiteSpace(isTable.TableComment))
        {
            tableComment = new Comment(isTable.TableComment);
        }

        TableIndexMetadata indexMetadata = await GetTableIndexMetadataAsync(catalog, schema, name);

        return new MyTable(tableId, columns)
        {
            PrimaryKey = indexMetadata.PrimaryKey,
            UniqueKeys = indexMetadata.UniqueKeys,
            ForeignKeys = await GetForeignKeysAsync(catalog, schema, name, tableId),
            Keys = indexMetadata.Keys,
            Checks = GetChecks(tableData.GetCheckRows(name)),
            Engine = isTable.Engine,
            CharacterSet = isTable.CharacterSet,
            Collation = isTable.Collation,
            TableComment = tableComment,
        };
    }

    private async Task<SchemaTableData> GetSchemaTableDataAsync(string catalog, string schema)
    {
        (string Catalog, string Schema) schemaKey = (catalog, schema);
        Lazy<Task<SchemaTableData>> lazyRows = _tableDataBySchema.GetOrAdd(
            schemaKey,
            _ => new Lazy<Task<SchemaTableData>>(
                () => LoadSchemaTableDataAsync(catalog, schema),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return await lazyRows.Value;
        }
        catch
        {
            _ = _tableDataBySchema.TryRemove(schemaKey, out _);
            throw;
        }
    }

    private async Task<SchemaTableData> LoadSchemaTableDataAsync(string catalog, string schema, CancellationToken cancellationToken = default)
    {
        await using QueryLease<InfoSchemaContext> lease = await GetConnectionAsync(cancellationToken);
        InfoSchemaContext context = lease.Context;

        List<TableDto> tableInfoRows = await (from t in context.Tables
                                              join coll in context.CollationCharacterSetApplicabilities
                                              on t.TableCollation equals coll.CollationName
                                              where t.TableCatalog == catalog
                                              && t.TableSchema == schema
                                              && t.TableType == "BASE TABLE"
                                              select new TableDto
                                              {
                                                  TableName = t.TableName,
                                                  Engine = t.Engine!,
                                                  CharacterSet = coll.CharacterSetName,
                                                  Collation = coll.CollationName,
                                                  TableComment = t.TableComment
                                              }).ToListAsync(cancellationToken);

        List<ColumnDto> columnRows = await (from c in context.Columns
                                            join ccSub in context.CheckConstraints
                                            on new { Cat = c.TableCatalog, Sch = c.TableSchema, Col = c.ColumnName }
                                                equals new { Cat = ccSub.ConstraintCatalog, Sch = ccSub.ConstraintSchema, Col = ccSub.ConstraintName }
                                                into ccGroup
                                            from cc in ccGroup.DefaultIfEmpty()
                                            where c.TableCatalog == catalog
                                            && c.TableSchema == schema
                                            orderby c.TableName, c.OrdinalPosition
                                            select new ColumnDto
                                            {
                                                TableName = c.TableName,
                                                ColumnName = c.ColumnName,
                                                ColumnType = c.ColumnType,
                                                CharacterSetName = c.CharacterSetName,
                                                CollationName = c.CollationName,
                                                IsNullable = c.IsNullable,
                                                ColumnDefault = c.ColumnDefault,
                                                GenerationExpression = c.GenerationExpression,
                                                Extra = c.Extra,
                                                ColumnComment = c.ColumnComment,
                                                CheckClause = cc.CheckClause
                                            }).ToListAsync(cancellationToken);

        List<CheckConstraintEntity> checkConstraints = await (from c in context.CheckConstraints
                                                              where c.ConstraintCatalog == catalog
                                                              && c.ConstraintSchema == schema
                                                              select c).ToListAsync(cancellationToken);

        List<TableConstraintEntity> checkTableConstraints = await (from tc in context.TableConstraints
                                                                   where tc.ConstraintCatalog == catalog
                                                                   && tc.ConstraintSchema == schema
                                                                   && tc.TableSchema == schema
                                                                   && tc.ConstraintType == "CHECK"
                                                                   select tc).ToListAsync(cancellationToken);

        Dictionary<string, string> checkClauseByName = checkConstraints.ToDictionary(c => c.ConstraintName, c => c.CheckClause);
        List<TableCheckDto> checkRows =
        [
            .. checkTableConstraints
                .Where(tc => checkClauseByName.ContainsKey(tc.ConstraintName))
                .Select(tc => new TableCheckDto
                {
                    TableName = tc.TableName,
                    ConstraintName = tc.ConstraintName,
                    CheckClause = checkClauseByName[tc.ConstraintName]
                })
        ];

        return new SchemaTableData(
            tableInfoRows.ToDictionary(t => t.TableName, t => t),
            columnRows.GroupBy(r => r.TableName).ToDictionary(g => g.Key, g => (IReadOnlyList<ColumnDto>)[.. g]),
            checkRows.GroupBy(r => r.TableName).ToDictionary(g => g.Key, g => (IReadOnlyList<TableCheckDto>)[.. g]));
    }

    private SqlValueList<MyColumn> GetColumns(IReadOnlyList<ColumnDto> list, ObjectIdentifier tableId)
    {
        var columns = new SqlValueList<MyColumn>();

        foreach (ColumnDto q in list)
        {
            var id = new ColumnIdentifier(q.ColumnName, tableId, _quoteStyle);

            DataType dataType = _helper.ParseDataTypeWithDefaults(
                q.ColumnType,
                q.CharacterSetName,
                q.CollationName);

            bool isNullable = q.IsNullable == "YES";
            ColumnOption.Nullability nullability = isNullable ? new ColumnOption.Nullability.Null() : new ColumnOption.Nullability.NotNull();

            ColumnOption.ColumnDefault? colDefault = _helper.ParseColumnDefault(q.ColumnDefault, isNullable);

            if (colDefault is ColumnOption.ColumnDefault.DefaultExpression
                {
                    Expression: SingleIdentifier { Identifier.QuoteStyle: QuoteStyle.None } idExpr
                }
                && ((dataType is MyDataType.MyChar ct && ct.Length >= idExpr.Identifier.Name.Length)
                    || (dataType is MyDataType.MyVarchar vc && vc.Length >= idExpr.Identifier.Name.Length)))
            {
                colDefault = new ColumnOption.ColumnDefault.DefaultValue(new Value.SingleQuotedString(idExpr.Identifier.Name));
            }

            ColumnOption.Generated? generation = _helper.ParseGenerated(q.GenerationExpression, q.Extra, _config.ParseSettings.RemoveSlashesBeforeQuotesGenerationExpression);

            ColumnOption.CheckConstraint? check = _helper.ParseColumnCheck(q.CheckClause);

            ColumnOption.OnUpdate? onUpdate = _helper.ParseOnUpdate(q.Extra);

            bool autoIncrement = q.Extra?.Contains("AUTO_INCREMENT", StringComparison.InvariantCultureIgnoreCase) ?? false;

            Comment? comment = null;
            if (!string.IsNullOrEmpty(q.ColumnComment))
            {
                comment = new Comment(q.ColumnComment);
            }

            columns.Add(new MyColumn(id, dataType)
            {
                Nullability = nullability,
                Default = colDefault,
                Generated = generation,
                Check = check,
                AutoIncrement = autoIncrement,
                OnUpdate = onUpdate,
                Comment = comment,
            });
        }

        return [.. columns];
    }

    private async Task<TableIndexMetadata> GetTableIndexMetadataAsync(string catalog, string schema, string tableName)
    {
        SchemaIndexData schemaIndexData = await GetSchemaIndexDataAsync(catalog, schema);
        return schemaIndexData.GetTableIndexMetadata(tableName);
    }

    private async Task<DatabaseComponentDict<MyForeignKey>> GetForeignKeysAsync(string catalog, string schema, string tableName, ObjectIdentifier tableId)
    {
        SchemaForeignKeyData foreignKeyData = await GetSchemaForeignKeyDataAsync(catalog, schema);
        return foreignKeyData.GetForeignKeys(tableName, tableId.Schema, _quoteStyle, _config.NameHandling);
    }

    private async Task<SchemaIndexData> GetSchemaIndexDataAsync(string catalog, string schema)
    {
        (string Catalog, string Schema) schemaKey = (catalog, schema);
        Lazy<Task<SchemaIndexData>> lazyRows = _indexDataBySchema.GetOrAdd(
            schemaKey,
            _ => new Lazy<Task<SchemaIndexData>>(
                () => LoadSchemaIndexDataAsync(catalog, schema),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return await lazyRows.Value;
        }
        catch
        {
            _ = _indexDataBySchema.TryRemove(schemaKey, out _);
            throw;
        }
    }

    private async Task<SchemaIndexData> LoadSchemaIndexDataAsync(string catalog, string schema, CancellationToken cancellationToken = default)
    {
        await using QueryLease<InfoSchemaContext> lease = await GetConnectionAsync(cancellationToken);
        InfoSchemaContext context = lease.Context;

        List<SchemaConstraintDto> constraints = await (from tc in context.TableConstraints
                                                       where tc.ConstraintCatalog == catalog
                                                       && tc.ConstraintSchema == schema
                                                       && tc.TableSchema == schema
                                                       && (tc.ConstraintType == "PRIMARY KEY" || tc.ConstraintType == "UNIQUE")
                                                       select new SchemaConstraintDto
                                                       {
                                                           TableName = tc.TableName,
                                                           ConstraintName = tc.ConstraintName,
                                                           ConstraintType = tc.ConstraintType
                                                       }).ToListAsync(cancellationToken);

        List<StatisticsDto> statisticsRows = await (from s in context.Statistics
                                                    where s.TableCatalog == catalog
                                                    && s.TableSchema == schema
                                                    && s.IndexSchema == schema
                                                    orderby s.TableName, s.IndexName, s.SeqInIndex
                                                    select new StatisticsDto
                                                    {
                                                        TableName = s.TableName,
                                                        IndexName = s.IndexName,
                                                        ColumnName = s.ColumnName,
                                                        IndexType = s.IndexType,
                                                        SubPart = s.SubPart,
                                                        Collation = s.Collation,
                                                        IndexComment = s.IndexComment,
                                                        SeqInIndex = s.SeqInIndex
                                                    }).ToListAsync(cancellationToken);

        Dictionary<string, Dictionary<string, string>> constraintTypesByTable = constraints
            .GroupBy(c => c.TableName)
            .ToDictionary(
                g => g.Key,
                g => g.ToDictionary(c => c.ConstraintName, c => c.ConstraintType));

        Dictionary<string, TableIndexMetadata> tableIndexMetadataByName = [];

        foreach (IGrouping<string, StatisticsDto> tableGroup in statisticsRows.GroupBy(r => r.TableName!))
        {
            constraintTypesByTable.TryGetValue(tableGroup.Key, out Dictionary<string, string>? constraintTypesByName);

            ILookup<string, IndexInfo> indexInfoByName = tableGroup
                .Select(r => StatisticsToIndexInfo(r, _quoteStyle))
                .ToLookup(i => i.IndexName);

            MyPrimaryKey? primaryKey = null;
            var uniqueKeys = new DatabaseComponentDict<MyUniqueKey>();
            var keys = new DatabaseComponentDict<MyKey>();

            foreach (IGrouping<string, IndexInfo> grouping in indexInfoByName)
            {
                List<IndexInfo> indexInfo = [.. grouping];

                if (constraintTypesByName?.TryGetValue(grouping.Key, out string? constraintType) == true)
                {
                    if (constraintType == "PRIMARY KEY")
                    {
                        KeyPart.Column[] keyColumns = [.. indexInfo.Select(s => new KeyPart.Column(s.ColumnName, s.SubPart, s.Collation))];
                        IndexInfo first = indexInfo.First();
                        (IndexMethod? indexMethod, Comment? comment) = first.GetIndexTypeAndComment(_lexer, _parser);

                        primaryKey = new MyPrimaryKey(keyColumns)
                        {
                            IndexMethod = indexMethod,
                            Comment = comment
                        };

                        continue;
                    }

                    if (constraintType == "UNIQUE")
                    {
                        var id = new Identifier(grouping.Key, _quoteStyle);
                        IndexInfo first = indexInfo.First();
                        (IndexMethod? indexMethod, Comment? comment) = first.GetIndexTypeAndComment(_lexer, _parser);
                        KeyPart.Column[] keyColumns = [.. indexInfo.Select(s => new KeyPart.Column(s.ColumnName, s.SubPart, s.Collation))];

                        uniqueKeys.Add(Handle.Create(id, _config.NameHandling), new MyUniqueKey(keyColumns, id)
                        {
                            IndexMethod = indexMethod,
                            Comment = comment,
                        });

                        continue;
                    }
                }

                KeyValuePair<Handle, MyKey> kvp = IndexInfoToKey(indexInfo);
                keys.Add(kvp.Key, kvp.Value);
            }

            tableIndexMetadataByName[tableGroup.Key] = new TableIndexMetadata(primaryKey, uniqueKeys, keys);
        }

        return new SchemaIndexData(tableIndexMetadataByName);
    }

    private async Task<SchemaForeignKeyData> GetSchemaForeignKeyDataAsync(string catalog, string schema)
    {
        (string Catalog, string Schema) schemaKey = (catalog, schema);
        Lazy<Task<SchemaForeignKeyData>> lazyRows = _foreignKeyDataBySchema.GetOrAdd(
            schemaKey,
            _ => new Lazy<Task<SchemaForeignKeyData>>(
                () => LoadSchemaForeignKeyDataAsync(catalog, schema),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return await lazyRows.Value;
        }
        catch
        {
            _ = _foreignKeyDataBySchema.TryRemove(schemaKey, out _);
            throw;
        }
    }

    private async Task<SchemaForeignKeyData> LoadSchemaForeignKeyDataAsync(string catalog, string schema, CancellationToken cancellationToken = default)
    {
        await using QueryLease<InfoSchemaContext> lease = await GetConnectionAsync(cancellationToken);
        InfoSchemaContext context = lease.Context;

        List<ForeignKeyColumnDto> rows = await (from tc in context.TableConstraints
                                                join rc in context.ReferentialConstraints
                                                on new { tc.ConstraintCatalog, tc.ConstraintSchema, tc.ConstraintName }
                                                    equals new { rc.ConstraintCatalog, rc.ConstraintSchema, rc.ConstraintName }
                                                join kcu in context.KeyColumnUsages
                                                on new { rc.ConstraintCatalog, rc.ConstraintSchema, rc.ConstraintName, rc.TableName }
                                                    equals new { kcu.ConstraintCatalog, kcu.ConstraintSchema, kcu.ConstraintName, kcu.TableName }
                                                where tc.ConstraintCatalog == catalog
                                                && tc.ConstraintSchema == schema
                                                && tc.TableSchema == schema
                                                && tc.ConstraintType == "FOREIGN KEY"
                                                orderby tc.TableName, rc.ConstraintName, kcu.OrdinalPosition
                                                select new ForeignKeyColumnDto
                                                {
                                                    TableName = tc.TableName,
                                                    ConstraintName = rc.ConstraintName,
                                                    UpdateRule = rc.UpdateRule,
                                                    DeleteRule = rc.DeleteRule,
                                                    ColumnName = kcu.ColumnName,
                                                    ReferencedTableSchema = kcu.ReferencedTableSchema,
                                                    ReferencedTableName = kcu.ReferencedTableName,
                                                    ReferencedColumnName = kcu.ReferencedColumnName
                                                }).ToListAsync(cancellationToken);

        return new SchemaForeignKeyData(
            rows.GroupBy(r => r.TableName).ToDictionary(g => g.Key, g => (IReadOnlyList<ForeignKeyColumnDto>)[.. g]));
    }

    private KeyValuePair<Handle, MyKey> IndexInfoToKey(List<IndexInfo> info)
    {
        IndexInfo first = info.First();
        var id = new Identifier(first.IndexName, _quoteStyle);
        var keyParts = new SqlValueList<KeyPart>([.. info.OrderBy(s => s.SeqInIndex).Select(s => new KeyPart.Column(s.ColumnName, s.SubPart, s.Collation))]);
        MyKey key;

        if (first.IndexType == "FULLTEXT")
        {
            key = new MyKey.FullText(keyParts, id);
        }
        else if (first.IndexType == "SPATIAL")
        {
            key = new MyKey.Spatial(keyParts, id);
        }
        else
        {
            IndexMethod indexMethod = IndexMethod.Parse(first.IndexType);
            key = new MyKey.Standard(keyParts, id)
            {
                IndexMethod = indexMethod
            };
        }

        return new KeyValuePair<Handle, MyKey>(Handle.Create(id, _config.NameHandling), key);
    }

    private DatabaseComponentDict<MyCheck> GetChecks(IReadOnlyList<TableCheckDto> isChecks)
    {
        DatabaseComponentDict<MyCheck> checks = [];

        foreach (TableCheckDto c in isChecks)
        {
            MyCheck check = _helper.ParseTableCheck(c.CheckClause, c.ConstraintName);
            checks.Add(Handle.Create(check.ConstraintName!, _config.NameHandling), check);
        }

        return checks;
    }

    private async Task<SchemaRoutineData> GetSchemaRoutineDataAsync(string catalog, string schema)
    {
        (string Catalog, string Schema) schemaKey = (catalog, schema);
        Lazy<Task<SchemaRoutineData>> lazyRows = _routineDataBySchema.GetOrAdd(
            schemaKey,
            _ => new Lazy<Task<SchemaRoutineData>>(
                () => LoadSchemaRoutineDataAsync(catalog, schema),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return await lazyRows.Value;
        }
        catch
        {
            _ = _routineDataBySchema.TryRemove(schemaKey, out _);
            throw;
        }
    }

    private async Task<SchemaRoutineData> LoadSchemaRoutineDataAsync(string catalog, string schema, CancellationToken cancellationToken = default)
    {
        await using QueryLease<InfoSchemaContext> lease = await GetConnectionAsync(cancellationToken);
        InfoSchemaContext context = lease.Context;

        List<RoutineRow> routineRows = await (from r in context.Routines
                                              where r.RoutineCatalog == catalog
                                              && r.RoutineSchema == schema
                                              select new RoutineRow
                                              {
                                                  Name = r.RoutineName,
                                                  Type = r.RoutineType,
                                                  Value = new RoutineDto
                                                  {
                                                      DataType = r.DtdIdentifier,
                                                      Definer = r.Definer,
                                                      SecurityType = r.SecurityType,
                                                      SqlDataAccess = r.SqlDataAccess,
                                                      RoutineComment = r.RoutineComment,
                                                      IsDeterministic = r.IsDeterministic,
                                                      RoutineDefinition = r.RoutineDefinition,
                                                  }
                                              }).ToListAsync(cancellationToken);

        List<RoutineParameterRow> parameterRows = await (from rp in context.Parameters
                                                         where rp.SpecificCatalog == catalog
                                                         && rp.SpecificSchema == schema
                                                         && rp.OrdinalPosition > 0
                                                         select new RoutineParameterRow
                                                         {
                                                             Name = rp.SpecificName,
                                                             Type = rp.RoutineType,
                                                             OrdinalPosition = checked((int)rp.OrdinalPosition),
                                                             Value = new RoutineParameterDto
                                                             {
                                                                 ParameterName = rp.ParameterName!,
                                                                 DataType = rp.DtdIdentifier,
                                                                 CharacterSet = rp.CharacterSetName,
                                                                 Collation = rp.CollationName,
                                                                 ParameterMode = rp.ParameterMode
                                                             }
                                                         }).ToListAsync(cancellationToken);

        return new SchemaRoutineData(
            routineRows.ToDictionary(r => (r.Name, r.Type), r => r.Value),
            parameterRows
                .OrderBy(r => r.OrdinalPosition)
                .GroupBy(r => (r.Name, r.Type))
                .ToDictionary(g => g.Key, g => (IReadOnlyList<RoutineParameterDto>)[.. g.Select(r => r.Value)]));
    }

    private SqlValueList<RoutineParameter> GetRoutineParameters(
        SchemaRoutineData routineData,
        string routineType,
        string routineName)
    {
        routineData.ParametersByRoutine.TryGetValue((routineName, routineType), out IReadOnlyList<RoutineParameterDto>? parameters);
        var routineParms = new SqlValueList<RoutineParameter>();
        foreach (RoutineParameterDto p in parameters ?? [])
        {
            DataType dataType = _helper.ParseDataTypeWithDefaults(p.DataType, p.CharacterSet, p.Collation);
            var name = new Identifier(p.ParameterName, _quoteStyle);
            RoutineParameterDirection? direction = routineType == "FUNCTION"
                ? null
                : RoutineParameterDirection.Parse(p.ParameterMode!);
            routineParms.Add(new RoutineParameter.Directed(name, dataType, direction));
        }

        return routineParms;
    }

    public async Task<MyStoredProcedure> GetProcedureAsync(ObjectIdentifier procedureId, CancellationToken cancellationToken = default)
    {
        string createProcText = (await _rawEntityRetriever.GetCreateProcedureAsync(procedureId, cancellationToken)).CreateProcedure!;
        var createProc = (CreateProcedure)_parser.Parse([.. _lexer.Tokenize(createProcText)])[0];
        string createProcBody = createProcText[(int)createProc.Body.Meta.Start!..];

        (string catalog, string schema, string procName) = procedureId.Strings;

        SchemaRoutineData routineData = await GetSchemaRoutineDataAsync(catalog, schema);
        if (!routineData.RoutinesByName.TryGetValue((procName, "PROCEDURE"), out RoutineDto proc))
        {
            throw new InvalidOperationException($"Failed to load metadata for procedure '{procedureId}'.");
        }

        SqlValueList<RoutineParameter> routineParams = GetRoutineParameters(routineData, "PROCEDURE", procName);

        RoutineInfo procInfo = RoutineToRoutineInfo(proc);

        Statement body = createProc.Body;

        return new MyStoredProcedure(procedureId, routineParams, body)
        {
            Definer = procInfo.Definer,
            SecurityContext = procInfo.SecurityContext,
            Comment = procInfo.Comment,
            Deterministic = procInfo.IsDeterministic,
            SqlDataRelation = procInfo.SqlDataRelation,
            RawBodyText = createProcBody,
        };
    }

    public async Task<MyStoredFunction> GetFunctionAsync(ObjectIdentifier functionId, CancellationToken cancellationToken = default)
    {
        (string catalog, string schema, string funcName) = functionId.Strings;
        SchemaRoutineData routineData = await GetSchemaRoutineDataAsync(catalog, schema);
        if (!routineData.RoutinesByName.TryGetValue((funcName, "FUNCTION"), out RoutineDto func))
        {
            throw new InvalidOperationException($"Failed to load metadata for function '{functionId}'.");
        }

        SqlValueList<RoutineParameter> routineParams = GetRoutineParameters(routineData, "FUNCTION", funcName);
        RoutineInfo funcInfo = RoutineToRoutineInfo(func);
        DataType returnType = _helper.ParseDataType(func.DataType!);

        Statement body = _helper.ParseStatement(func.RoutineDefinition!);

        bool isAggregate = _helper.IsAggregateFunction(func.RoutineDefinition!);

        return new MyStoredFunction(functionId, routineParams, returnType, body)
        {
            Definer = funcInfo.Definer,
            SecurityContext = funcInfo.SecurityContext,
            Aggregate = isAggregate,
            Comment = funcInfo.Comment,
            Deterministic = funcInfo.IsDeterministic,
            SqlDataRelation = funcInfo.SqlDataRelation,
            RawBodyText = func.RoutineDefinition!,
        };
    }

    public async Task<Dictionary<ObjectIdentifier, MyTrigger>> GetTriggersOnTableAsync(ObjectIdentifier tableId, CancellationToken cancellationToken = default)
    {
        (string catalog, string schema, string name) = tableId.Strings;

        Dictionary<ObjectIdentifier, MyTrigger> triggers = [];

        (string Catalog, string Schema) schemaKey = (catalog, schema);
        Lazy<Task<List<TriggerDto>>> lazyRows = _triggersBySchema.GetOrAdd(
            schemaKey,
            _ => new Lazy<Task<List<TriggerDto>>>(
                () => LoadSchemaTriggersAsync(catalog, schema, cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication));

        List<TriggerDto> schemaRows;
        try
        {
            schemaRows = await lazyRows.Value;
        }
        catch
        {
            _ = _triggersBySchema.TryRemove(schemaKey, out _);
            throw;
        }

        List<TriggerDto> tableRows = [.. schemaRows.Where(t => t.EventObjectTable == name)];

        var isTrigGroups = from t in tableRows
                           group t by new { t.ActionTiming, t.EventManipulation } into g
                           select g;

        foreach (var group in isTrigGroups)
        {
            foreach (TriggerDto isTrig in group)
            {
                var objId = new ObjectIdentifier(isTrig.TriggerName, tableId.Schema, _quoteStyle);

                var trigTime = TriggerTime.Parse(isTrig.ActionTiming);
                var trigEvent = TriggerEvent.Parse(isTrig.EventManipulation);

                Statement body = _helper.ParseStatement(isTrig.ActionStatement);

                triggers[objId] = new MyTrigger(objId, tableId, trigTime, trigEvent, body)
                {
                    Definer = new Definer(_helper.ParseAccount(isTrig.Definer)),
                    Order = (uint)isTrig.ActionOrder,
                    RawBodyText = isTrig.ActionStatement
                };
            }
        }

        return triggers;
    }

    private async Task<List<TriggerDto>> LoadSchemaTriggersAsync(string catalog, string schema, CancellationToken cancellationToken = default)
    {
        await using QueryLease<InfoSchemaContext> lease = await GetConnectionAsync(cancellationToken);
        InfoSchemaContext context = lease.Context;

        return await (from t in context.Triggers
                      where t.EventObjectCatalog == catalog
                      && t.EventObjectSchema == schema
                      orderby t.EventObjectTable, t.ActionOrder
                      select new TriggerDto
                      {
                          EventObjectTable = t.EventObjectTable,
                          TriggerName = t.TriggerName,
                          EventManipulation = t.EventManipulation,
                          ActionTiming = t.ActionTiming,
                          ActionStatement = t.ActionStatement,
                          ActionOrder = t.ActionOrder,
                          Definer = t.Definer
                      }).ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, ViewEntity>> GetViewsBySchemaAsync(string catalog, string schema, CancellationToken cancellationToken = default)
    {
        (string Catalog, string Schema) schemaKey = (catalog, schema);
        Lazy<Task<IReadOnlyDictionary<string, ViewEntity>>> lazyViews = _viewsBySchema.GetOrAdd(
            schemaKey,
            _ => new Lazy<Task<IReadOnlyDictionary<string, ViewEntity>>>(
                () => LoadViewsBySchemaAsync(catalog, schema, cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return await lazyViews.Value;
        }
        catch
        {
            _ = _viewsBySchema.TryRemove(schemaKey, out _);
            throw;
        }
    }

    private async Task<IReadOnlyDictionary<string, ViewEntity>> LoadViewsBySchemaAsync(string catalog, string schema, CancellationToken cancellationToken)
    {
        await using QueryLease<InfoSchemaContext> lease = await GetConnectionAsync(cancellationToken);
        InfoSchemaContext context = lease.Context;

        List<ViewEntity> views = await context.Views
            .Where(v => v.TableCatalog == catalog && v.TableSchema == schema)
            .ToListAsync(cancellationToken);
        return views.ToDictionary(v => v.TableName);
    }

    public async Task<MyView> GetViewAsync(ObjectIdentifier viewId, CancellationToken cancellationToken = default)
    {
        (string catalog, string schema, string viewName) = viewId.Strings;
        IReadOnlyDictionary<string, ViewEntity> views = await GetViewsBySchemaAsync(catalog, schema, cancellationToken);
        if (!views.TryGetValue(viewName, out ViewEntity? isView))
        {
            throw new InvalidOperationException($"Failed to load metadata for view '{viewId}'.");
        }

        var body = (Select)_helper.ParseStatement(isView.ViewDefinition);
        SecurityContext securityContext = SecurityContext.Parse(isView.SecurityType);
        string createViewEntity = (await _rawEntityRetriever.GetCreateViewAsync(viewId, cancellationToken)).CreateView!;

        ViewAlgorithm? algorithm = null;
        Match agloMatch = s_algorithmRegex.Match(createViewEntity);

        if (agloMatch.Success)
        {
            algorithm = ViewAlgorithm.Parse(agloMatch.Groups[1].Value);
        }

        ViewCheckOption? checkOption = null;
        Match checkMatch = s_checkOptionRegex.Match(createViewEntity);
        if (checkMatch.Success)
        {
            checkOption = string.IsNullOrWhiteSpace(checkMatch.Groups[1].Value)
                ? ViewCheckOption.Cascaded
                : ViewCheckOption.Parse(checkMatch.Groups[1].Value);
        }


        return new MyView(viewId, body)
        {
            Definer = new Definer(_helper.ParseAccount(isView.Definer)),
            SecurityContext = securityContext,
            Algorithm = algorithm,
            CheckOption = checkOption,
            RawBodyText = isView.ViewDefinition,
        };
    }

    private async Task<IReadOnlyDictionary<string, EventEntity>> GetEventsBySchemaAsync(string catalog, string schema, CancellationToken cancellationToken = default)
    {
        (string Catalog, string Schema) schemaKey = (catalog, schema);
        Lazy<Task<IReadOnlyDictionary<string, EventEntity>>> lazyEvents = _eventsBySchema.GetOrAdd(
            schemaKey,
            _ => new Lazy<Task<IReadOnlyDictionary<string, EventEntity>>>(
                () => LoadEventsBySchemaAsync(catalog, schema, cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return await lazyEvents.Value;
        }
        catch
        {
            _ = _eventsBySchema.TryRemove(schemaKey, out _);
            throw;
        }
    }

    private async Task<IReadOnlyDictionary<string, EventEntity>> LoadEventsBySchemaAsync(string catalog, string schema, CancellationToken cancellationToken)
    {
        await using QueryLease<InfoSchemaContext> lease = await GetConnectionAsync(cancellationToken);
        InfoSchemaContext context = lease.Context;

        List<EventEntity> events = await context.Events
            .Where(e => e.EventCatalog == catalog && e.EventSchema == schema)
            .ToListAsync(cancellationToken);
        return events.ToDictionary(e => e.EventName);
    }

    public async Task<MyEvent> GetEventAsync(ObjectIdentifier eventId, CancellationToken cancellationToken = default)
    {
        (string catalog, string schema, string eventName) = eventId.Strings;
        IReadOnlyDictionary<string, EventEntity> events = await GetEventsBySchemaAsync(catalog, schema, cancellationToken);
        if (!events.TryGetValue(eventName, out EventEntity? isEvent))
        {
            throw new InvalidOperationException($"Failed to load metadata for event '{eventId}'.");
        }

        Statement body = _helper.ParseStatement(isEvent.EventDefinition);

        EventSchedule schedule;
        if (isEvent.ExecuteAt != null)
        {
            schedule = new EventSchedule.At(isEvent.ExecuteAt.Value.ToLiteralValueExpression());
        }
        else
        {
            Expression? start = isEvent.Starts.ToLiteralValueExpression();
            Expression? end = isEvent.Ends.ToLiteralValueExpression();
            Expression quantity = _helper.ParseExpr(isEvent.IntervalValue!);
            var field = DateTimeUnit.Parse(isEvent.IntervalField!);

            schedule = new EventSchedule.Every(quantity, field)
            {
                Start = start,
                End = end
            };
        }

        bool onCompletionPreserve = isEvent.OnCompletion == "PRESERVE";

        EventEnabledStatus status = isEvent.Status.ToUpperInvariant() switch
        {
            "ENABLED" => EventEnabledStatus.Enable,
            "DISABLED" => EventEnabledStatus.Disable,
            "REPLICA_SIDE_DISABLED" => EventEnabledStatus.DisableOnReplica,
            _ => throw new UnreachableException($"Event status {isEvent.Status}")
        };


        return new MyEvent(eventId, schedule, body)
        {
            Definer = new Definer(_helper.ParseAccount(isEvent.Definer)),
            OnCompletionPreserve = onCompletionPreserve,
            EventEnabledStatus = status,
            Comment = string.IsNullOrWhiteSpace(isEvent.EventComment) ? null : new Comment(isEvent.EventComment),
            RawBodyText = isEvent.EventDefinition,
        };
    }

    public async Task<List<string>> GetUnappliedRefactorsAsync(SchemaIdentifier schemaId, IReadOnlyCollection<string> allRefactorIds, CancellationToken cancellationToken = default)
    {
        if (allRefactorIds.Count == 0)
        {
            return [];
        }

        string fullTableName = await CreateManagementTableAsync(schemaId, cancellationToken);

        string formattedRefactorIds = string.Join(',', allRefactorIds.Select(id => $"ROW('{id}')"));
        string existsQuery = $"""
                           VALUES {formattedRefactorIds}
                           EXCEPT
                           SELECT `entry_key` FROM {fullTableName}
                           WHERE `entry_type` = '{StoredMetadataConstants.Refactor}'
                           """;
        await using QueryLease<InfoSchemaContext> lease = await GetConnectionAsync(cancellationToken);
        InfoSchemaContext context = lease.Context;

        List<string> result = await context.Database.SqlQueryRaw<string>(existsQuery).ToListAsync(cancellationToken);
        return [.. result];
    }

    public async Task<List<string>> GetUnappliedDeployScriptsAsync(IReadOnlyCollection<SchemaIdentifier> schemaIds, IReadOnlyCollection<string> allDeployScriptIds, CancellationToken cancellationToken = default)
    {
        if (allDeployScriptIds.Count == 0)
        {
            return [];
        }

        var unionBuilder = new StringBuilder();
        foreach (SchemaIdentifier schema in schemaIds)
        {
            string fullTableName = await CreateManagementTableAsync(schema, cancellationToken);
            if (unionBuilder.Length > 0)
            {
                unionBuilder.AppendLine("UNION ALL");
            }
            unionBuilder.AppendLine(CultureInfo.InvariantCulture, $"""
                SELECT `entry_key` FROM {fullTableName}
                WHERE `entry_type` = '{StoredMetadataConstants.DeployScript}'
                """);
        }

        string formattedDeployScriptIds = string.Join(',', allDeployScriptIds.Select(id => $"ROW('{id}')"));
        string existsQuery = $"""
                           VALUES {formattedDeployScriptIds}
                           EXCEPT
                           ({unionBuilder})
                           """;
        await using QueryLease<InfoSchemaContext> lease = await GetConnectionAsync(cancellationToken);
        InfoSchemaContext context = lease.Context;

        return await context.Database.SqlQueryRaw<string>(existsQuery).ToListAsync(cancellationToken);
    }

    private async Task<string> CreateManagementTableAsync(SchemaIdentifier schemaId, CancellationToken cancellationToken = default)
    {
        await using QueryLease<InfoSchemaContext> lease = await GetConnectionAsync(cancellationToken);
        InfoSchemaContext context = lease.Context;

        IQueryable<TableEntity> tableExistsQuery =
            from t in context.Tables
            where t.TableCatalog == schemaId.Catalog.Name
            && t.TableSchema == schemaId.Name
            && t.TableName == StoredMetadataConstants.TableName
            select t;

        string fullTableName = $"`{schemaId.Name}`.`{StoredMetadataConstants.TableName}`";

        if (await tableExistsQuery.AnyAsync(cancellationToken))
        {
            return fullTableName;
        }

        string createTableQuery = $"""
            CREATE TABLE {fullTableName} (
                `entry_key` CHAR(36) NOT NULL,
                `entry_type` CHAR(1) NOT NULL,
                UNIQUE KEY `{StoredMetadataConstants.TableName}_entry_key` (`entry_key`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
            """;
        await context.Database.ExecuteSqlRawAsync(createTableQuery, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return fullTableName;
    }

    private static IndexInfo StatisticsToIndexInfo(StatisticsDto e, QuoteStyle quoteStyle)
    {
        uint? subPart = (uint?)e.SubPart;
        Direction? collation = null;
        if (e.Collation == "A")
        {
            collation = Direction.Ascending;
        }
        else if (e.Collation == "D")
        {
            collation = Direction.Descending;
        }
        return new IndexInfo()
        {
            ColumnName = new Identifier(e.ColumnName, quoteStyle),
            SubPart = subPart,
            Collation = collation,
            IndexType = e.IndexType,
            IndexComment = e.IndexComment,
            IndexName = e.IndexName,
            SeqInIndex = e.SeqInIndex
        };
    }

    private RoutineInfo RoutineToRoutineInfo(RoutineDto e)
    {
        bool isDeterministic = e.IsDeterministic.Equals("yes", StringComparison.OrdinalIgnoreCase);
        Comment? comment = null;
        if (!string.IsNullOrEmpty(e.RoutineComment))
        {
            comment = new Comment(e.RoutineComment);
        }
        SecurityContext securityContext = SecurityContext.Parse(e.SecurityType);
        var definer = new Definer(_helper.ParseAccount(e.Definer));
        var relation = SqlDataRelation.Parse(e.SqlDataAccess);

        return new RoutineInfo()
        {
            Definer = definer,
            SecurityContext = securityContext,
            IsDeterministic = isDeterministic,
            SqlDataRelation = relation,
            Comment = comment,
        };
    }

    private async Task<QueryLease<InfoSchemaContext>> GetConnectionAsync(CancellationToken cancellationToken)
    {
        await _queryGate.WaitAsync(cancellationToken);
        try
        {
            InfoSchemaContext context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return new QueryLease<InfoSchemaContext>(context, _queryGate);
        }
        catch
        {
            _queryGate.Release();
            throw;
        }
    }

    public void Dispose()
    {
        _queryGate.Dispose();
        GC.SuppressFinalize(this);
    }


    [GeneratedRegex(@"^\s*CREATE\s+ALGORITHM\s*=\s*(\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex GetAlgorithmRegex();

    [GeneratedRegex(@"\s+WITH\s+(?:(\w+)\s+)?CHECK\s+OPTION\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex GetCheckOptionRegex();
}
