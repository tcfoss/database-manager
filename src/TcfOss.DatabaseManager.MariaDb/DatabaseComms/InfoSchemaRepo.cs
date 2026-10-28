using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseComms;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.MariaDb.DatabaseComms.EntityFramework;
using TcfOss.DatabaseManager.MariaDb.DatabaseComms.EntityFramework.Entities;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DatabaseComms;
using TcfOss.DatabaseManager.MySql.DatabaseComms.InfoSchemaHelperModels;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;
using TcfOss.DatabaseManager.MySql.DatabaseObjects.Components;
using TcfOss.DatabaseManager.MySql.Lexing;
using TcfOss.DatabaseManager.MySql.Parsing;

namespace TcfOss.DatabaseManager.MariaDb.DatabaseComms;

public class InfoSchemaRepo : IRetrieveDatabaseObjects, IDisposable
{
    private readonly SemaphoreSlim _queryGate = new(10);
    private readonly CachedDataLoader<ILookup<string, TriggerDto>> _triggersBySchema;
    private readonly CachedDataLoader<IReadOnlyDictionary<string, TableData>> _tableDataBySchema;
    private readonly CachedDataLoader<SchemaRoutineData> _routineDataBySchema;
    private readonly CachedDataLoader<IReadOnlyDictionary<string, ViewEntity>> _viewsBySchema;
    private readonly CachedDataLoader<IReadOnlyDictionary<string, EventEntity>> _eventsBySchema;

    private readonly QuoteStyle _quoteStyle;
    private readonly MyConfig _config;
    private readonly MyLexer _lexer;
    private readonly MyParser _parser;
    private readonly InfoSchemaRepoHelper _helper;

    private readonly IDbContextFactory<InfoSchemaContext> _contextFactory;
    private readonly IRetrieveRawEntities _rawEntityRetriever;


    public InfoSchemaRepo(MyConfig config, IDbContextFactory<InfoSchemaContext> contextFactory, IRetrieveRawEntities rawEntityRetriever)
    {
        _contextFactory = contextFactory;
        _rawEntityRetriever = rawEntityRetriever;
        _config = config;
        _quoteStyle = config.QuoteStyle;
        _lexer = new MyLexer();
        _parser = new MyParser();
        _helper = new InfoSchemaRepoHelper(_lexer, _parser, _quoteStyle);
        _triggersBySchema = new CachedDataLoader<ILookup<string, TriggerDto>>(LoadSchemaTriggersAsync);
        _tableDataBySchema = new CachedDataLoader<IReadOnlyDictionary<string, TableData>>(LoadSchemaTableDataAsync);
        _routineDataBySchema = new CachedDataLoader<SchemaRoutineData>(LoadSchemaRoutineDataAsync);
        _viewsBySchema = new CachedDataLoader<IReadOnlyDictionary<string, ViewEntity>>(LoadSchemaViewsAsync);
        _eventsBySchema = new CachedDataLoader<IReadOnlyDictionary<string, EventEntity>>(LoadSchemaEventsAsync);
    }

    public async Task<List<ObjectIdentifier>> GetTableIdentifiersAsync(SchemaIdentifier schemaId, IEnumerable<string> excludedNames, bool refreshCache = true, CancellationToken cancellationToken = default)
    {
        if (refreshCache)
        {
            // Refresh schema-local caches for each schema load.
            _triggersBySchema.Clear(schemaId.InfoSchemaKey);
            _tableDataBySchema.Clear(schemaId.InfoSchemaKey);
            _routineDataBySchema.Clear(schemaId.InfoSchemaKey);
            _viewsBySchema.Clear(schemaId.InfoSchemaKey);
            _eventsBySchema.Clear(schemaId.InfoSchemaKey);
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

    public Task<List<ObjectIdentifier>> GetProcedureIdentifiersAsync(SchemaIdentifier schemaId, IEnumerable<string> excludedNames, CancellationToken cancellationToken = default)
    {
        return GetRoutineIdentifiersAsync(schemaId, "PROCEDURE", excludedNames, cancellationToken);
    }

    public Task<List<ObjectIdentifier>> GetFunctionIdentifiersAsync(SchemaIdentifier schemaId, IEnumerable<string> excludedNames, CancellationToken cancellationToken = default)
    {
        return GetRoutineIdentifiersAsync(schemaId, "FUNCTION", excludedNames, cancellationToken);
    }

    private async Task<List<ObjectIdentifier>> GetRoutineIdentifiersAsync(SchemaIdentifier schemaId, string routineType, IEnumerable<string> excludedNames, CancellationToken cancellationToken)
    {
        await using QueryLease<InfoSchemaContext> lease = await GetConnectionAsync(cancellationToken);
        InfoSchemaContext context = lease.Context;

        IQueryable<ObjectIdentifier> query = from r in context.Routines
                                             where r.RoutineCatalog == schemaId.Catalog.Name
                                             && r.RoutineSchema == schemaId.Name
                                             && r.RoutineType == routineType
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
        string name = tableId.Name;

        IReadOnlyDictionary<string, TableData> tables = await _tableDataBySchema.GetAsync(tableId.InfoSchemaKey, cancellationToken);
        TableData tableData = tables[name];
        TableDto isTable = tableData.Info;

        SqlValueList<MyColumn> columns = GetColumns(tableData.Columns, tableId);
        Comment? tableComment = null;
        if (!string.IsNullOrWhiteSpace(isTable.TableComment))
        {
            tableComment = new Comment(isTable.TableComment);
        }

        TableIndexMetadata indexMetadata = tableData.Indexes;

        return new MyTable(tableId, columns)
        {
            PrimaryKey = indexMetadata.PrimaryKey,
            UniqueKeys = indexMetadata.UniqueKeys,
            ForeignKeys = tableData.GetForeignKeys(tableId.Schema, _quoteStyle, _config.NameHandling),
            Keys = indexMetadata.Keys,
            Checks = GetChecks(tableData.Checks),
            Engine = isTable.Engine,
            CharacterSet = isTable.CharacterSet,
            Collation = isTable.Collation,
            TableComment = tableComment,
        };
    }

    private async Task<IReadOnlyDictionary<string, TableData>> LoadSchemaTableDataAsync(InfoSchemaKey infoSchemaKey, CancellationToken cancellationToken = default)
    {
        await using QueryLease<InfoSchemaContext> lease = await GetConnectionAsync(cancellationToken);
        InfoSchemaContext context = lease.Context;

        List<TableDto> tableInfoRows = await (from t in context.Tables
                                              join coll in context.CollationCharacterSetApplicabilities
                                              on t.TableCollation equals coll.FullCollationName
                                              where t.TableCatalog == infoSchemaKey.Catalog
                                              && t.TableSchema == infoSchemaKey.Schema
                                              && t.TableType == "BASE TABLE"
                                              select new TableDto
                                              {
                                                  TableName = t.TableName,
                                                  Engine = t.Engine!,
                                                  CharacterSet = coll.CharacterSetName,
                                                  Collation = coll.FullCollationName,
                                                  TableComment = t.TableComment
                                              }).ToListAsync(cancellationToken);

        List<ColumnDto> columnRows = await (from c in context.Columns
                                            join ccSub in context.CheckConstraints
                                            on new { c.TableCatalog, c.TableSchema, Tab = c.TableName, Level = "Column", Col = c.ColumnName }
                                                equals new { TableCatalog = ccSub.ConstraintCatalog, TableSchema = ccSub.ConstraintSchema, Tab = ccSub.TableName, ccSub.Level, Col = ccSub.ConstraintName }
                                                into ccGroup
                                            from cc in ccGroup.DefaultIfEmpty()
                                            where c.TableCatalog == infoSchemaKey.Catalog
                                            && c.TableSchema == infoSchemaKey.Schema
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

        List<TableCheckDto> checkRows = await (from c in context.CheckConstraints
                                               where c.ConstraintCatalog == infoSchemaKey.Catalog
                                               && c.ConstraintSchema == infoSchemaKey.Schema
                                               select new TableCheckDto
                                               {
                                                   TableName = c.TableName,
                                                   ConstraintName = c.ConstraintName,
                                                   CheckClause = c.CheckClause
                                               }).ToListAsync(cancellationToken);

        Dictionary<string, TableIndexMetadata> indexMetadata = await LoadIndexMetadataAsync(context, infoSchemaKey, cancellationToken);
        ILookup<string, ForeignKeyColumnDto> foreignKeyRows = await LoadForeignKeyRowsAsync(context, infoSchemaKey, cancellationToken);
        ILookup<string, ColumnDto> columnsByTable = columnRows.ToLookup(r => r.TableName);
        ILookup<string, TableCheckDto> checksByTable = checkRows.ToLookup(r => r.TableName);

        return tableInfoRows.ToDictionary(
            t => t.TableName,
            t => new TableData(
                t,
                [.. columnsByTable[t.TableName]],
                [.. checksByTable[t.TableName]],
                indexMetadata.GetValueOrDefault(t.TableName) ?? new TableIndexMetadata(null, [], []),
                [.. foreignKeyRows[t.TableName]]));
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

            ColumnOption.Generated? generation = _helper.ParseGenerated(q.GenerationExpression, q.Extra, _config.ParseSettings.RemoveSlashesBeforeQuotesGenerationExpression);

            ColumnOption.CheckConstraint? check = _helper.ParseColumnCheck(q.CheckClause);

            ColumnOption.OnUpdate? onUpdate = _helper.ParseOnUpdate(q.Extra);

            bool autoIncrement = q.Extra.Contains("AUTO_INCREMENT", StringComparison.InvariantCultureIgnoreCase);

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

        return columns;
    }

    private async Task<Dictionary<string, TableIndexMetadata>> LoadIndexMetadataAsync(InfoSchemaContext context, InfoSchemaKey infoSchemaKey, CancellationToken cancellationToken)
    {
        List<SchemaConstraintDto> constraints = await (from tc in context.TableConstraints
                                                       where tc.ConstraintCatalog == infoSchemaKey.Catalog
                                                       && tc.ConstraintSchema == infoSchemaKey.Schema
                                                       && tc.TableSchema == infoSchemaKey.Schema
                                                       && (tc.ConstraintType == "PRIMARY KEY" || tc.ConstraintType == "UNIQUE")
                                                       select new SchemaConstraintDto
                                                       {
                                                           TableName = tc.TableName,
                                                           ConstraintName = tc.ConstraintName,
                                                           ConstraintType = tc.ConstraintType
                                                       }).ToListAsync(cancellationToken);

        List<StatisticsDto> statisticsRows = await (from s in context.Statistics
                                                    where s.TableCatalog == infoSchemaKey.Catalog
                                                    && s.TableSchema == infoSchemaKey.Schema
                                                    && s.IndexSchema == infoSchemaKey.Schema
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

        foreach (IGrouping<string, StatisticsDto> tableGroup in statisticsRows.GroupBy(r => r.TableName))
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
                string? constraintType = null;
                constraintTypesByName?.TryGetValue(grouping.Key, out constraintType);

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
                }
                else if (constraintType == "UNIQUE")
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
                }
                else
                {
                    KeyValuePair<Handle, MyKey> kvp = _helper.IndexInfoToKey(indexInfo, _config.NameHandling);
                    keys.Add(kvp.Key, kvp.Value);
                }
            }

            tableIndexMetadataByName[tableGroup.Key] = new TableIndexMetadata(primaryKey, uniqueKeys, keys);
        }

        return tableIndexMetadataByName;
    }

    private static async Task<ILookup<string, ForeignKeyColumnDto>> LoadForeignKeyRowsAsync(InfoSchemaContext context, InfoSchemaKey infoSchemaKey, CancellationToken cancellationToken)
    {
        List<ForeignKeyColumnDto> rows = await (from tc in context.TableConstraints
                                                join rc in context.ReferentialConstraints
                                                on new { tc.ConstraintCatalog, tc.ConstraintSchema, tc.ConstraintName }
                                                    equals new { rc.ConstraintCatalog, rc.ConstraintSchema, rc.ConstraintName }
                                                join kcu in context.KeyColumnUsages
                                                on new { rc.ConstraintCatalog, rc.ConstraintSchema, rc.ConstraintName, rc.TableName }
                                                    equals new { kcu.ConstraintCatalog, kcu.ConstraintSchema, kcu.ConstraintName, kcu.TableName }
                                                where tc.ConstraintCatalog == infoSchemaKey.Catalog
                                                && tc.ConstraintSchema == infoSchemaKey.Schema
                                                && tc.TableSchema == infoSchemaKey.Schema
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

        return rows.ToLookup(r => r.TableName);
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

    private async Task<SchemaRoutineData> LoadSchemaRoutineDataAsync(InfoSchemaKey infoSchemaKey, CancellationToken cancellationToken = default)
    {
        await using QueryLease<InfoSchemaContext> lease = await GetConnectionAsync(cancellationToken);
        InfoSchemaContext context = lease.Context;

        List<RoutineRow> routineRows = await (from r in context.Routines
                                              where r.RoutineCatalog == infoSchemaKey.Catalog
                                              && r.RoutineSchema == infoSchemaKey.Schema
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
                                                         where rp.SpecificCatalog == infoSchemaKey.Catalog
                                                         && rp.SpecificSchema == infoSchemaKey.Schema
                                                         && rp.OrdinalPosition > 0
                                                         select new RoutineParameterRow
                                                         {
                                                             Name = rp.SpecificName,
                                                             Type = rp.RoutineType,
                                                             OrdinalPosition = rp.OrdinalPosition,
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
            RoutineParameterDirection direction = RoutineParameterDirection.Parse(p.ParameterMode!);
            routineParms.Add(new RoutineParameter.Directed(name, dataType, direction));
        }

        return routineParms;
    }

    public async Task<MyStoredProcedure> GetProcedureAsync(ObjectIdentifier procedureId, CancellationToken cancellationToken = default)
    {
        string createProcText = (await _rawEntityRetriever.GetCreateProcedureAsync(procedureId, cancellationToken)).CreateProcedure!;
        var createProc = (CreateProcedure)_parser.Parse([.. _lexer.Tokenize(createProcText)])[0];
        string createProcBody = createProcText[(int)createProc.Body.Meta.Start!..];

        string procName = procedureId.Name;
        SchemaRoutineData routineData = await _routineDataBySchema.GetAsync(procedureId.InfoSchemaKey, cancellationToken);

        RoutineDto proc = routineData.RoutinesByName[(procName, "PROCEDURE")];

        SqlValueList<RoutineParameter> routineParams = GetRoutineParameters(routineData, "PROCEDURE", procName);

        RoutineInfo procInfo = _helper.RoutineToRoutineInfo(proc);

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
        string funcName = functionId.Name;
        SchemaRoutineData routineData = await _routineDataBySchema.GetAsync(functionId.InfoSchemaKey, cancellationToken);

        RoutineDto func = routineData.RoutinesByName[(funcName, "FUNCTION")];

        SqlValueList<RoutineParameter> routineParams = GetRoutineParameters(routineData, "FUNCTION", funcName);
        RoutineInfo funcInfo = _helper.RoutineToRoutineInfo(func);
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
        return _helper.TriggersOnTableToDictionary((await _triggersBySchema.GetAsync(tableId.InfoSchemaKey, cancellationToken))[tableId.Name], tableId);
    }

    private async Task<ILookup<string, TriggerDto>> LoadSchemaTriggersAsync(InfoSchemaKey schemaKey, CancellationToken cancellationToken = default)
    {
        await using QueryLease<InfoSchemaContext> lease = await GetConnectionAsync(cancellationToken);
        InfoSchemaContext context = lease.Context;

        List<TriggerDto> triggers = await (from t in context.Triggers
                                           where t.EventObjectCatalog == schemaKey.Catalog
                                           && t.EventObjectSchema == schemaKey.Schema
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
        return triggers.ToLookup(t => t.EventObjectTable);
    }

    private async Task<IReadOnlyDictionary<string, ViewEntity>> LoadSchemaViewsAsync(InfoSchemaKey schemaKey, CancellationToken cancellationToken)
    {
        await using QueryLease<InfoSchemaContext> lease = await GetConnectionAsync(cancellationToken);
        InfoSchemaContext context = lease.Context;

        List<ViewEntity> views = await context.Views
            .Where(v => v.TableCatalog == schemaKey.Catalog && v.TableSchema == schemaKey.Schema)
            .ToListAsync(cancellationToken);
        return views.ToDictionary(v => v.TableName);
    }

    public async Task<MyView> GetViewAsync(ObjectIdentifier viewId, CancellationToken cancellationToken = default)
    {
        string viewName = viewId.Name;
        IReadOnlyDictionary<string, ViewEntity> views = await _viewsBySchema.GetAsync(viewId.InfoSchemaKey, cancellationToken);
        ViewEntity isView = views[viewName];

        var body = (Select)_helper.ParseStatement(isView.ViewDefinition);

        SecurityContext securityContext = SecurityContext.Parse(isView.SecurityType);
        ViewCheckOption? checkOption = isView.CheckOption == "NONE" ? null : ViewCheckOption.Parse(isView.CheckOption);
        ViewAlgorithm algorithm = ViewAlgorithm.Parse(isView.Algorithm);

        return new MyView(viewId, body)
        {
            Definer = new Definer(_helper.ParseAccount(isView.Definer)),
            SecurityContext = securityContext,
            Algorithm = algorithm,
            CheckOption = checkOption,
            RawBodyText = isView.ViewDefinition,
        };
    }

    private async Task<IReadOnlyDictionary<string, EventEntity>> LoadSchemaEventsAsync(InfoSchemaKey schemaKey, CancellationToken cancellationToken)
    {
        await using QueryLease<InfoSchemaContext> lease = await GetConnectionAsync(cancellationToken);
        InfoSchemaContext context = lease.Context;

        List<EventEntity> events = await context.Events
            .Where(e => e.EventCatalog == schemaKey.Catalog && e.EventSchema == schemaKey.Schema)
            .ToListAsync(cancellationToken);
        return events.ToDictionary(e => e.EventName);
    }

    public async Task<MyEvent> GetEventAsync(ObjectIdentifier eventId, CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<string, EventEntity> events = await _eventsBySchema.GetAsync(eventId.InfoSchemaKey, cancellationToken);
        EventEntity isEvent = events[eventId.Name];

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
            "SLAVESIDE_DISABLED" => EventEnabledStatus.DisableOnSlave,
            _ => throw new DefensiveGuardException("event status", isEvent.Status)
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

    public async Task<HashSet<string>> GetUnappliedRefactorsAsync(IReadOnlyCollection<SchemaIdentifier> schemaIds, IReadOnlyCollection<string> allRefactorIds, CancellationToken cancellationToken = default)
    {
        if (schemaIds.Count == 0 || allRefactorIds.Count == 0)
        {
            return [];
        }
        await CreateManagementTablesAsync(schemaIds, cancellationToken);

        string query = InfoSchemaRepoHelper.GetUnappliedEntriesSql(schemaIds, allRefactorIds.Count, StoredMetadataConstants.Refactor, includeRowKeyword: false);

        await using QueryLease<InfoSchemaContext> lease = await GetConnectionAsync(cancellationToken);
        InfoSchemaContext context = lease.Context;

        List<string> result = await context.Database.SqlQueryRaw<string>(query, [.. allRefactorIds]).ToListAsync(cancellationToken);
        return [.. result];
    }

    public async Task<List<string>> GetUnappliedDeployScriptsAsync(IReadOnlyCollection<SchemaIdentifier> schemaIds, IReadOnlyCollection<string> allDeployScriptIds, CancellationToken cancellationToken = default)
    {
        if (schemaIds.Count == 0 || allDeployScriptIds.Count == 0)
        {
            return [];
        }
        await CreateManagementTablesAsync(schemaIds, cancellationToken);

        string query = InfoSchemaRepoHelper.GetUnappliedEntriesSql(schemaIds, allDeployScriptIds.Count, StoredMetadataConstants.DeployScript, includeRowKeyword: false);

        await using QueryLease<InfoSchemaContext> lease = await GetConnectionAsync(cancellationToken);
        InfoSchemaContext context = lease.Context;

        return await context.Database.SqlQueryRaw<string>(query, [.. allDeployScriptIds]).ToListAsync(cancellationToken);
    }

    private async Task CreateManagementTablesAsync(IReadOnlyCollection<SchemaIdentifier> schemaIds, CancellationToken cancellationToken)
    {
        await _queryGate.WaitAsync(cancellationToken);
        try
        {
            await using InfoSchemaContext context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            List<string> schemaNames = [.. schemaIds.Select(s => s.Name)];

            var existingSchemas = (await (from t in context.Tables
                                          where schemaNames.Contains(t.TableSchema)
                                                && t.TableName == StoredMetadataConstants.TableName
                                          select t.TableSchema).ToListAsync(cancellationToken)).ToHashSet();

            foreach (SchemaIdentifier schemaId in schemaIds)
            {
                if (!existingSchemas.Contains(schemaId.Name))
                {
                    string fullTableName = InfoSchemaRepoHelper.GetMetadataTableName(schemaId);
                    string createTableQuery = InfoSchemaRepoHelper.GetMetadataTableCreateSql(fullTableName);
                    await context.Database.ExecuteSqlRawAsync(createTableQuery, cancellationToken);
                }
            }
        }
        finally
        {
            _queryGate.Release();
        }
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

    [ExcludeFromCodeCoverage(Justification = "Catch block is impossible to cover; Happy path is covered by consumers.")]
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
        _triggersBySchema.Dispose();
        _tableDataBySchema.Dispose();
        _routineDataBySchema.Dispose();
        _viewsBySchema.Dispose();
        _eventsBySchema.Dispose();
        _queryGate.Dispose();
        GC.SuppressFinalize(this);
    }
}
