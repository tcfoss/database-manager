using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;
using TcfOss.DatabaseManager.MySql.BuiltIn;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;
using TcfOss.DatabaseManager.MySql.DatabaseObjects.Components;
using TcfOss.DatabaseManager.MySql.StatementAnalysis;

namespace TcfOss.DatabaseManager.MySql.DefinitionBuilding;

public partial class MyDefinitionBuilder
    : DefinitionBuilder<MyDefinition, MyConfig, MySchemaMapping>
{
    private readonly Dictionary<ObjectHandle, MyTableBuilder> _tableBuilders = [];
    private readonly DatabaseObjectDict<MyStoredProcedure> _procedures;
    private readonly DatabaseObjectDict<MyStoredFunction> _functions;
    private readonly DatabaseObjectDict<MyPreView> _preViews;
    private readonly DatabaseObjectDict<MyEvent> _events;
    protected Dictionary<PreTriggerKey, List<MyPreTrigger>> PreTriggers { get; } = [];

    private readonly List<(CreateIndex Statement, SchemaIdentifier Schema, SourceRef? SourceRef)> _pendingIndexes = [];

    private readonly ExtendedQuoteStyle _accountQuoteStyle;
    private readonly MyComponentNormalizer _componentNormalizer;
    private readonly MyNormalizer _expressionNormalizer;
    private readonly ValidationHelper _validationHelper;

    private readonly Dictionary<SchemaIdentifier, BuilderHelpers> _helpers = [];


    public MyDefinitionBuilder(MyConfig config, SourceManager sourceManager, IFunctionNameProvider functionNameProvider, ILogger<MyDefinitionBuilder> logger)
        : base(config, sourceManager, logger)
    {
        _accountQuoteStyle = config.NormalizationSettings.AccountQuoteStyle;
        _componentNormalizer = new MyComponentNormalizer(config.QuoteStyle, functionNameProvider);
        _expressionNormalizer = new MyNormalizer(config, _componentNormalizer);
        _validationHelper = new ValidationHelper(logger);

        _procedures = new DatabaseObjectDict<MyStoredProcedure>(config.NameHandling);
        _functions = new DatabaseObjectDict<MyStoredFunction>(config.NameHandling);
        _preViews = new DatabaseObjectDict<MyPreView>(config.NameHandling);
        _events = new DatabaseObjectDict<MyEvent>(config.NameHandling);

        foreach (MySchemaMapping schemaMapping in config.Schemas.Values)
        {
            var dtNormalizer = new MyDataTypeNormalizer(config, schemaMapping.SchemaDefaults.CharacterSet, schemaMapping.SchemaDefaults.Collation);
            var attrNormalizer = new MyAttributeNormalizer(config, _componentNormalizer, dtNormalizer);
            _helpers[schemaMapping.SchemaName] = new BuilderHelpers
            {
                ProcedureConstructor = new MyConstructProcedure(config, attrNormalizer, sourceManager),
                FunctionConstructor = new MyConstructFunction(config, attrNormalizer, sourceManager),
                TriggerConstructor = new MyConstructPreTrigger(attrNormalizer, sourceManager),
                ViewConstructor = new MyConstructPreView(config, attrNormalizer, sourceManager),
                EventConstructor = new MyConstructEvent(config, attrNormalizer, sourceManager)
            };
        }
    }

    public override void ProcessStatements(IEnumerable<Statement> statements, SchemaIdentifier activeSchemaId, int sourceId)
    {
        s_logProcessingFile(Logger, new LogDelegateGetFileWrapper(SourceManager, new SourceRef(sourceId, 0, 0)));

        foreach (Statement statement in statements)
        {
            if (statement.Meta.Start == null || statement.Meta.End == null)
            {
                throw new InvalidOperationException($"Statement is missing source position metadata: {statement.GetType().Name}");
            }
            var sourceRef = new SourceRef(sourceId, statement.Meta.Start.Value, statement.Meta.End.Value);

            switch (statement)
            {
                case CreateTable ct:
                    ObjectNameAndHandle tableNameAndHandle = AddTable(ct, activeSchemaId, sourceRef);
                    SourceManager.RegisterObject(tableNameAndHandle.Key, sourceRef);
                    break;
                case CreateProcedure cp:
                    ObjectNameAndHandle procedureNameAndHandle = AddProcedure(cp, activeSchemaId, sourceRef);
                    SourceManager.RegisterObject(procedureNameAndHandle.Key, sourceRef);
                    break;
                case CreateFunction cf:
                    ObjectNameAndHandle functionNameAndHandle = AddFunction(cf, activeSchemaId, sourceRef);
                    SourceManager.RegisterObject(functionNameAndHandle.Key, sourceRef);
                    break;
                case CreateTrigger ct:
                    ObjectNameAndHandle triggerNameAndHandle = AddTrigger(ct, activeSchemaId, sourceRef);
                    SourceManager.RegisterObject(triggerNameAndHandle.Key, sourceRef);
                    break;
                case CreateView cv:
                    ObjectNameAndHandle viewNameAndHandle = AddView(cv, activeSchemaId, sourceRef);
                    SourceManager.RegisterObject(viewNameAndHandle.Key, sourceRef);
                    break;
                case CreateEvent ce:
                    ObjectNameAndHandle eventNameAndHandle = AddEvent(ce, activeSchemaId, sourceRef);
                    SourceManager.RegisterObject(eventNameAndHandle.Key, sourceRef);
                    break;
                case CreateIndex ci:
                    QueuePendingIndex(ci, activeSchemaId, sourceRef);
                    break;
                case Use u:
                    if (u.UseObject != UseObject.Database && u.UseObject != UseObject.Schema && u.UseObject != null)
                    {
                        throw new SqlSyntaxException($"MySQL only supports USE <DATABASE> or USE <SCHEMA>. Got USE {u.UseObject}.", sourceRef);
                    }
                    activeSchemaId = SchemaIdentifier.FromObjectName(u.ObjectName, Config.Catalog, QuoteStyle);
                    break;
                case InertOnly:
                    // Ignore
                    break;
                default:
                    throw new DefinitionException($"{statement.GetType().Name} statement", sourceRef);
            }
        }
    }

    public override MyDefinition ToDefinition()
    {
        ApplyPendingIndexes();

        List<PseudoTable> pseudoTables = GetPseudoTables();

        var definition = new MyDefinition
        {
            Tables = GetTables(),
            Triggers = GetTriggers(),
            Views = GetViews(pseudoTables),
            Events = _events,
            Procedures = _procedures,
            Functions = _functions
        };

        List<SourcedException> errors = ValidateDefinition(definition);
        if (errors.Count > 0)
        {
            throw new ValidationExceptionSet(errors);
        }

        return definition;
    }

    protected override List<SourcedException> ValidateDefinition(MyDefinition definition)
    {
        s_logValidatingDefinition(Logger);
        var errors = new List<SourcedException>();
        foreach (MyTable table in definition.Tables.Values)
        {
            errors.AddRange(ValidateTable(table, definition));
        }
        return errors;
    }

    protected override List<PseudoTable> GetPseudoTables()
    {
        var pseudoTables = new List<PseudoTable>();
        foreach (MyTableBuilder tableBuilder in _tableBuilders.Values)
        {
            pseudoTables.Add(tableBuilder.ToPseudoTable());
        }
        foreach (MyPreView preView in _preViews.Values)
        {
            pseudoTables.Add(preView.Body.ToPseudoTable(preView.Name.Name, preView.Name, preView.SourceRef, PseudoTableType.View));
        }
        return pseudoTables;
    }

    private MyTableBuilder ConstructTableBuilder(CreateTable createTableStatement, ObjectIdentifier tableId, SourceRef? sourceRef)
    {
        var builder = new MyTableBuilder(tableId, Config, Config.Schemas[tableId.Schema].SchemaDefaults, _componentNormalizer, _expressionNormalizer, sourceRef);
        ValidateCreateTable(createTableStatement, tableId, sourceRef);
        builder.ApplyCreateTable(createTableStatement);
        return builder;
    }

    private ObjectNameAndHandle AddTable(CreateTable createTableStatement, SchemaIdentifier currentSchema, SourceRef? sourceRef = null)
    {
        ObjectNameAndHandle tableNameAndHandle = GetAndValidateName(createTableStatement, null, currentSchema, sourceRef);
        MyTableBuilder tableBuilder = ConstructTableBuilder(createTableStatement, tableNameAndHandle.Id, sourceRef);
        _tableBuilders.Add(tableNameAndHandle.Key, tableBuilder);
        s_logAddedObject(Logger, "table", tableNameAndHandle.Id, tableNameAndHandle.Id.Schema, new LogDelegateGetFileWrapper(SourceManager, sourceRef));
        return tableNameAndHandle;
    }

    private MyStoredProcedure ConstructProcedure(CreateProcedure createProcedureStatement, ObjectIdentifier procedureId, SchemaIdentifier currentSchema, SourceRef? sourceRef)
    {
        if (createProcedureStatement.Definer == null && Config.DefaultDefiner != null)
        {
            createProcedureStatement = createProcedureStatement with { Definer = Config.DefaultDefiner };
        }
        ValidateDefiner(createProcedureStatement.Definer, "Procedure", procedureId, sourceRef);
        ValidateSecurityContext(createProcedureStatement.MyCharacteristic?.SecurityContext, "Procedure", procedureId, sourceRef);

        return _helpers[currentSchema].ProcedureConstructor.ConstructProcedure(createProcedureStatement, procedureId, _accountQuoteStyle, sourceRef);
    }

    private ObjectNameAndHandle AddProcedure(CreateProcedure createProcedureStatement, SchemaIdentifier currentSchema, SourceRef? sourceRef = null)
    {
        ObjectNameAndHandle procedureNameAndHandle = GetAndValidateName(createProcedureStatement, null, currentSchema, sourceRef);
        MyStoredProcedure proc = ConstructProcedure(createProcedureStatement, procedureNameAndHandle.Id, currentSchema, sourceRef);
        _procedures[procedureNameAndHandle.Key] = proc;
        s_logAddedObject(Logger, "procedure", procedureNameAndHandle.Id, procedureNameAndHandle.Id.Schema, new LogDelegateGetFileWrapper(SourceManager, sourceRef));
        return procedureNameAndHandle;
    }

    private MyStoredFunction ConstructFunction(CreateFunction createFunctionStatement, ObjectIdentifier functionId, SchemaIdentifier currentSchema, SourceRef? sourceRef)
    {
        if (createFunctionStatement.Definer == null && Config.DefaultDefiner != null)
        {
            createFunctionStatement = createFunctionStatement with { Definer = Config.DefaultDefiner };
        }
        ValidateDefiner(createFunctionStatement.Definer, "Function", functionId, sourceRef);
        ValidateSecurityContext(createFunctionStatement.MyCharacteristic?.SecurityContext, "Function", functionId, sourceRef);

        return _helpers[currentSchema].FunctionConstructor.ConstructFunction(createFunctionStatement, functionId, _accountQuoteStyle, sourceRef);
    }

    private ObjectNameAndHandle AddFunction(CreateFunction createFunctionStatement, SchemaIdentifier currentSchema, SourceRef? sourceRef)
    {
        ObjectNameAndHandle functionNameAndHandle = GetAndValidateName(createFunctionStatement, null, currentSchema, sourceRef);
        MyStoredFunction func = ConstructFunction(createFunctionStatement, functionNameAndHandle.Id, currentSchema, sourceRef);
        _functions[functionNameAndHandle.Key] = func;
        s_logAddedObject(Logger, "function", functionNameAndHandle.Id, functionNameAndHandle.Id.Schema, new LogDelegateGetFileWrapper(SourceManager, sourceRef));
        return functionNameAndHandle;
    }

    private MyPreTrigger ConstructTrigger(CreateTrigger createTriggerStatement, ObjectIdentifier triggerId, SchemaIdentifier currentSchema, SourceRef? sourceRef)
    {
        MyPreTrigger preTrigger = _helpers[currentSchema].TriggerConstructor.ConstructTrigger(createTriggerStatement, triggerId, QuoteStyle, _accountQuoteStyle, sourceRef);
        return preTrigger;
    }

    private ObjectNameAndHandle AddTrigger(CreateTrigger createTriggerStatement, SchemaIdentifier currentSchema, SourceRef? sourceRef)
    {
        ObjectNameAndHandle triggerNameAndHandle = GetAndValidateName(createTriggerStatement, null, currentSchema, sourceRef);

        if (createTriggerStatement.Definer == null && Config.DefaultDefiner != null)
        {
            createTriggerStatement = createTriggerStatement with { Definer = Config.DefaultDefiner };
        }

        MyPreTrigger preTrigger = ConstructTrigger(createTriggerStatement, triggerNameAndHandle.Id, currentSchema, sourceRef);

        var triggerKey = new PreTriggerKey(preTrigger.OnTable, preTrigger.TriggerTime, preTrigger.TriggerEvent);
        List<MyPreTrigger>? preTriggers = ValidatePreTrigger(createTriggerStatement, preTrigger, sourceRef);
        if (preTriggers != null)
        {
            preTriggers.Add(preTrigger);
        }
        else
        {
            PreTriggers.Add(triggerKey, [preTrigger]);
        }
        s_logAddedObject(Logger, "trigger", triggerNameAndHandle.Id, triggerNameAndHandle.Id.Schema, new LogDelegateGetFileWrapper(SourceManager, sourceRef));
        return triggerNameAndHandle;
    }

    private MyPreView ConstructView(CreateView createViewStatement, ObjectIdentifier viewId, SchemaIdentifier currentSchema, SourceRef? sourceRef)
    {
        if (createViewStatement.Definer == null && Config.DefaultDefiner != null)
        {
            createViewStatement = createViewStatement with { Definer = Config.DefaultDefiner };
        }
        ValidateDefiner(createViewStatement.Definer, "View", viewId, sourceRef);
        ValidateSecurityContext(createViewStatement.SecurityContext, "View", viewId, sourceRef);

        return _helpers[currentSchema].ViewConstructor.ConstructPreView(createViewStatement, viewId, _accountQuoteStyle, sourceRef);
    }

    private ObjectNameAndHandle AddView(CreateView createViewStatement, SchemaIdentifier currentSchema, SourceRef? sourceRef)
    {
        ObjectNameAndHandle viewNameAndHandle = GetAndValidateName(createViewStatement, null, currentSchema, sourceRef);
        MyPreView view = ConstructView(createViewStatement, viewNameAndHandle.Id, currentSchema, sourceRef);
        _preViews[viewNameAndHandle.Key] = view;
        s_logAddedObject(Logger, "view", viewNameAndHandle.Id, viewNameAndHandle.Id.Schema, new LogDelegateGetFileWrapper(SourceManager, sourceRef));
        return viewNameAndHandle;
    }

    private MyEvent ConstructEvent(CreateEvent createEventStatement, ObjectIdentifier eventId, SchemaIdentifier currentSchema, SourceRef? sourceRef = null)
    {
        if (createEventStatement.Definer == null && Config.DefaultDefiner != null)
        {
            createEventStatement = createEventStatement with { Definer = Config.DefaultDefiner };
        }
        ValidateDefiner(createEventStatement.Definer, "Event", eventId, sourceRef);

        return _helpers[currentSchema].EventConstructor.ConstructEvent(createEventStatement, eventId, _accountQuoteStyle, sourceRef);
    }

    private ObjectNameAndHandle AddEvent(CreateEvent createEventStatement, SchemaIdentifier currentSchema, SourceRef? sourceRef = null)
    {
        ObjectNameAndHandle eventNameAndHandle = GetAndValidateName(createEventStatement, null, currentSchema, sourceRef);
        MyEvent evt = ConstructEvent(createEventStatement, eventNameAndHandle.Id, currentSchema, sourceRef);
        _events[eventNameAndHandle.Key] = evt;
        s_logAddedObject(Logger, "event", eventNameAndHandle.Id, eventNameAndHandle.Id.Schema, new LogDelegateGetFileWrapper(SourceManager, sourceRef));
        return eventNameAndHandle;
    }

    private DatabaseObjectDict<MyTable> GetTables()
    {
        var tables = new DatabaseObjectDict<MyTable>();
        foreach ((ObjectHandle tableKey, MyTableBuilder tableBuilder) in _tableBuilders)
        {
            tables[tableKey] = tableBuilder.ToTable();
        }
        return tables;
    }

    protected virtual DatabaseObjectDict<MyTrigger> GetTriggers()
    {
        var triggers = new DatabaseObjectDict<MyTrigger>(Config.NameHandling);
        foreach (List<MyPreTrigger> preTriggers in PreTriggers.Values)
        {
            var orderedPreTriggers = new List<MyPreTrigger>();

            MyPreTrigger? lastAfterTrigger = null;
            while (true)
            {
                // SingleOrDefault is slower, and the effect should be guaranteed by
                // the Duplicate FOLLOWS exception conditionally thrown in
                // AddTrigger.
                lastAfterTrigger = preTriggers.FirstOrDefault(x => x.AfterTrigger == lastAfterTrigger?.Name);
                if (lastAfterTrigger == null)
                {
                    break;
                }
                orderedPreTriggers.Add(lastAfterTrigger);
            }

            for (int i = 0; i < orderedPreTriggers.Count; i++)
            {
                MyPreTrigger curr = orderedPreTriggers[i];
                triggers[curr.Name] = new MyTrigger(curr.Name, curr.OnTable, curr.TriggerTime, curr.TriggerEvent, curr.Body)
                {
                    Definer = curr.Definer,
                    Order = (uint)(i + 1),
                    RawBodyText = curr.RawBodyText
                };
            }
        }

        return triggers;
    }

    private DatabaseObjectDict<MyView> GetViews(List<PseudoTable> pseudoTables)
    {
        Normalizer normalizer = new MyNormalizer(Config, _componentNormalizer);
        var views = new DatabaseObjectDict<MyView>();
        foreach ((ObjectHandle preViewKey, MyPreView preView) in _preViews)
        {
            MyView view = preView.ToNormalized(preView.Name.Schema, pseudoTables, normalizer);
            views.Add(preViewKey, view);
        }
        return views;
    }

    private void QueuePendingIndex(CreateIndex statement, SchemaIdentifier schema, SourceRef? sourceRef)
    {
        _pendingIndexes.Add((statement, schema, sourceRef));
    }

    private void ApplyPendingIndexes()
    {
        foreach ((CreateIndex statement, SchemaIdentifier schema, SourceRef? sourceRef) in _pendingIndexes)
        {
            ObjectIdentifier tableId = ObjectIdentifier.FromObjectName(statement.TableName, schema, QuoteStyle);
            ObjectHandle tableKey = ObjectHandle.Create(tableId, Config.NameHandling);
            if (!_tableBuilders.TryGetValue(tableKey, out MyTableBuilder? tableBuilder))
            {
                throw new SqlSyntaxException.CreateIndexUnknownTable(tableId, sourceRef);
            }

            switch (statement)
            {
                case CreateIndex.Unique u:
                    tableBuilder.AddUniqueKey(u.ToStatementConstraint(), sourceRef);
                    break;
                case CreateIndex.Standard s:
                    tableBuilder.AddKey(s.ToStatementConstraint(), sourceRef);
                    break;
                case CreateIndex.FullText f:
                    tableBuilder.AddKey(f.ToStatementConstraint(), sourceRef);
                    break;
                case CreateIndex.Spatial sp:
                    tableBuilder.AddKey(sp.ToStatementConstraint(), sourceRef);
                    break;
                default:
                    throw new SqlSyntaxException.CreateIndexUnexpectedType(statement.GetType().Name, sourceRef);
            }
        }
        _pendingIndexes.Clear();
    }

    #region Validation Helpers
    protected virtual void ValidateDefiner(Definer? definer, string objectType, ObjectIdentifier objectId, SourceRef? sourceRef)
    {
        if (definer == null)
        {
            throw new DefinitionException.MissingDefiner(objectType, objectId, sourceRef);
        }
        if (definer.Account is Account.CurrentRole or Account.CurrentUser)
        {
            throw new DefinitionException.ImplicitDefiner(objectType, objectId, sourceRef);
        }
    }

    protected virtual void ValidateSecurityContext(SecurityContext? securityContext, string objectType, ObjectIdentifier objectId, SourceRef? sourceRef)
    {
        if (securityContext == null)
        {
            throw new DefinitionException.MissingSecurityContext(objectType, objectId, sourceRef);
        }
    }


    protected virtual void ValidateCreateTable(CreateTable createTableStatement, ObjectIdentifier tableId, SourceRef? sourceRef)
    {
        if (createTableStatement.AsSelect != null)
        {
            throw new DefinitionException.CreateTableAsSelect(tableId, sourceRef);
        }

        if (!createTableStatement.Columns.SafeAny())
        {
            throw new DefinitionException.MissingTableColumns(tableId, sourceRef);
        }
    }

    protected virtual List<MyPreTrigger>? ValidatePreTrigger(CreateTrigger createTriggerStatement, MyPreTrigger preTrigger, SourceRef? sourceRef)
    {
        ValidateDefiner(createTriggerStatement.Definer, "Trigger", preTrigger.Name, sourceRef);
        if (preTrigger.BeforeTrigger != null)
        {
            throw new DefinitionException.TriggerOrderViaPrecedes(preTrigger.Name, sourceRef);
        }

        var triggerKey = new PreTriggerKey(preTrigger.OnTable, preTrigger.TriggerTime, preTrigger.TriggerEvent);
        if (PreTriggers.TryGetValue(triggerKey, out List<MyPreTrigger>? preTriggers))
        {
            MyPreTrigger? otherTrigger = preTriggers.FirstOrDefault(x => x.AfterTrigger == preTrigger.AfterTrigger);
            if (otherTrigger != null)
            {
                throw new DefinitionException.UndefinedTriggerOrder(preTrigger.Name, otherTrigger.Name, sourceRef);
            }
            return preTriggers;
        }
        return null;
    }

    private List<SourcedException> ValidateTable(MyTable table, MyDefinition definition)
    {
        SourceRef? sourceRef = SourceManager.GetSourceRef(ObjectHandle.Create(table.Name, Config.NameHandling));
        var errors = new List<SourcedException>();

        foreach (MyColumn column in table.Columns)
        {
            if (column.AutoIncrement)
            {
                if (table.PrimaryKey == null || !table.PrimaryKey.Columns.Any(x => x is KeyPart.Column keyCol && keyCol.Name.Name == column.Name.Name))
                {
                    errors.Add(new SqlSyntaxException.AutoIncrementPrimaryKeyOnly(column.Name, sourceRef));
                }
                if (column.DataType.Class != DataTypeClass.Integral)
                {
                    errors.Add(new SqlSyntaxException.AutoIncrementIntegerOnly(column.Name, sourceRef));
                }
            }
        }

        errors.AddRange(ValidateForeignRelations(table, definition, sourceRef));

        errors.AddRange(ValidateIndexColumnExistence(table, sourceRef));

        foreach (MyColumn col in table.Columns)
        {
            if (col.DataType is MyDataType.BaseMyStringType stringType)
            {
                if (stringType.StringAttribute == null)
                {
                    errors.Add(new DefinitionException.StringAttributeMissing(col.Name, sourceRef));
                    continue;
                }

                if (stringType.StringAttribute.CharacterSet == null)
                {
                    errors.Add(new DefinitionException.StringAttributeCharacterSetMissing(col.Name, sourceRef));
                    continue;
                }
                if (stringType.StringAttribute.Collation == null)
                {
                    errors.Add(new DefinitionException.StringAttributeCollationMissing(col.Name, sourceRef));
                    continue;
                }

                if (!Config.CharacterSets.TryGetValue(stringType.StringAttribute.CharacterSet, out CharacterSetSpec? value))
                {
                    errors.Add(new DefinitionException.StringAttributeInvalidCharacterSet(col.Name, stringType.StringAttribute.CharacterSet, sourceRef));
                }
                else if (!value.Collations.Contains(stringType.StringAttribute.Collation))
                {
                    errors.Add(new DefinitionException.StringAttributeInvalidCollation(col.Name, stringType.StringAttribute.CharacterSet, stringType.StringAttribute.Collation, sourceRef));
                }
            }
        }

        return errors;
    }


    private bool MatchingColumnListExists(List<Identifier> requiredColumns, MyTable table, string? extractionReason)
    {
        return _validationHelper.GetValidMatchingKeyPartList(requiredColumns, table.GetPotentialKeyPartLists(), extractionReason) != null;
    }

    private static bool DataTypesMatch(DataType? dt1, DataType? dt2)
    {
        if (dt1 == null || dt2 == null)
        {
            return false;
        }

        if (dt1.Class != dt2.Class)
        {
            return false;
        }

        return true;
    }

    private static List<SourcedException> ValidateForeignKeyIndexColumnsExist(MyForeignKey key, MyTable table, SourceRef? sourceRef)
    {
        var errors = new List<SourcedException>();

        foreach (Identifier keyCol in key.Columns)
        {
            if (!table.Columns.Any(x => x.Name.Name == keyCol.Name))
            {
                errors.Add(new SqlSyntaxException.IndexColumnNotFound(key.ConstraintName ?? "UNNAMED KEY", table.Name, keyCol, sourceRef));
            }
        }

        return errors;
    }

    protected virtual List<SourcedException> ValidateForeignKeyBackingIndexExists(MyForeignKey key, MyTable table, SourceRef? sourceRef)
    {
        var errors = new List<SourcedException>();

        if (!MatchingColumnListExists(key.Columns, table, $"FK {key.ConstraintName ?? "UNNAMED KEY"} backing index"))
        {
            errors.Add(new DefinitionException.ForeignKeyNoBackingIndex(key.ConstraintName ?? "UNNAMED KEY", table.Name, sourceRef));
        }

        return errors;
    }

    protected virtual List<SourcedException> ValidateForeignKeyReferencedColumnsBackingIndexExists(MyForeignKey key, MyTable table, MyTable referencedTable, SourceRef? sourceRef)
    {
        var errors = new List<SourcedException>();
        if (!MatchingColumnListExists(key.ReferencedColumns, referencedTable, $"FK {key.ConstraintName ?? "UNNAMED KEY"} referenced backing index"))
        {
            errors.Add(new DefinitionException.ForeignKeyNoReferencedBackingIndex(key.ConstraintName ?? "UNNAMED KEY", table.Name, key.ReferencedTable, sourceRef));
        }
        return errors;
    }


    private static List<SourcedException> ValidateForeignKeyReferencedDataTypesMatch(MyForeignKey key, MyTable table, MyTable referencedTable, SourceRef? sourceRef)
    {
        var errors = new List<SourcedException>();

        var localCols = key.Columns.Select(keyCol => table.Columns.FirstOrDefault(col => col.Name.Name == keyCol.Name)).Where(c => c != null).ToList();
        var foreignCols = key.ReferencedColumns.Select(keyCol => referencedTable.Columns.FirstOrDefault(col => col.Name.Name == keyCol.Name)).Where(c => c != null).ToList();

        if (localCols.Count == foreignCols.Count)
        {
            foreach ((MyColumn? local, MyColumn? other) in localCols.Zip(foreignCols))
            {
                if (!DataTypesMatch(local!.DataType, other!.DataType))
                {
                    errors.Add(new SqlSyntaxException.ForeignKeyColumnTypeMismatch(key.ConstraintName ?? "UNNAMED KEY", table.Name, local.Name.ToSimpleIdentifier(), local.DataType, key.ReferencedTable, other.Name.ToSimpleIdentifier(), other.DataType, sourceRef));
                }
            }
        }

        return errors;
    }

    private List<SourcedException> ValidateForeignKeyReferencedColumnsValid(MyForeignKey key, MyTable table, MyDefinition definition, SourceRef? sourceRef)
    {
        var errors = new List<SourcedException>();

        MyTable? referencedTable = definition.Tables.GetValueOrDefault(key.ReferencedTable);
        if (referencedTable == null)
        {
            errors.Add(new SqlSyntaxException.ForeignKeyReferencedTableNotFound(key.ConstraintName ?? "UNNAMED KEY", table.Name, key.ReferencedTable, sourceRef));
            return errors;
        }

        if (key.Columns.Count != key.ReferencedColumns.Count)
        {
            errors.Add(new SqlSyntaxException.ForeignKeyColumnCountMismatch(key.ConstraintName ?? "UNNAMED KEY", table.Name, key.Columns.Count, key.ReferencedColumns.Count, sourceRef));
            return errors;
        }

        foreach (Identifier keyCol in key.ReferencedColumns)
        {
            if (!referencedTable.Columns.Any(x => x.Name.Name == keyCol.Name))
            {
                errors.Add(new SqlSyntaxException.ForeignKeyReferencedColumnNotFound(key.ConstraintName ?? "UNNAMED KEY", table.Name, key.ReferencedTable, keyCol, sourceRef));
            }
        }

        errors.AddRange(ValidateForeignKeyReferencedColumnsBackingIndexExists(key, table, referencedTable, sourceRef));

        errors.AddRange(ValidateForeignKeyReferencedDataTypesMatch(key, table, referencedTable, sourceRef));

        return errors;
    }

    private List<SourcedException> ValidateForeignRelations(MyTable table, MyDefinition definition, SourceRef? sourceRef)
    {
        var errors = new List<SourcedException>();

        foreach (MyForeignKey foreignKey in table.ForeignKeys.Values)
        {
            errors.AddRange(ValidateForeignKeyIndexColumnsExist(foreignKey, table, sourceRef));
            errors.AddRange(ValidateForeignKeyBackingIndexExists(foreignKey, table, sourceRef));
            errors.AddRange(ValidateForeignKeyReferencedColumnsValid(foreignKey, table, definition, sourceRef));
        }

        return errors;
    }

    private static List<SourcedException> ValidateIndexColumnExistence(MyTable table, SourceRef? sourceRef)
    {
        var errors = new List<SourcedException>();

        foreach (KeyPart keyPart in table.PrimaryKey?.Columns ?? [])
        {
            if (keyPart is KeyPart.Column columnPart && !table.Columns.Any(x => x.Name.Name == columnPart.Name.Name))
            {
                errors.Add(new SqlSyntaxException.IndexColumnNotFound("PRIMARY", table.Name, columnPart.Name, sourceRef));
            }
        }

        foreach (MyUniqueKey uniqueKey in table.UniqueKeys.Values)
        {
            foreach (KeyPart keyPart in uniqueKey.Columns)
            {
                if (keyPart is KeyPart.Column columnPart && !table.Columns.Any(x => x.Name.Name == columnPart.Name.Name))
                {
                    errors.Add(new SqlSyntaxException.IndexColumnNotFound(uniqueKey.ConstraintName!, table.Name, columnPart.Name, sourceRef));
                }
            }
        }

        foreach (MyKey key in table.Keys.Values)
        {
            foreach (KeyPart keyPart in key.Columns)
            {
                if (keyPart is KeyPart.Column columnPart && !table.Columns.Any(x => x.Name.Name == columnPart.Name.Name))
                {
                    errors.Add(new SqlSyntaxException.IndexColumnNotFound(key.IndexName!, table.Name, columnPart.Name, sourceRef));
                }
            }
        }

        return errors;
    }
    #endregion

    #region Log Delegates
    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Processing statements from file '{FileName}'")]
    protected static partial void s_logProcessingFile(ILogger logger, LogDelegateGetFileWrapper fileName);

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Validating definition...")]
    protected static partial void s_logValidatingDefinition(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Added {ObjectType} {ObjectId} to schema {SchemaId} from file {FileName}")]
    protected static partial void s_logAddedObject(ILogger logger, string objectType, string objectId, SchemaIdentifier schemaId, LogDelegateGetFileWrapper fileName);
    #endregion
}
