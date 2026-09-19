using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseComms;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.DefinitionMapping;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.MySql.BuiltIn;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;
using TcfOss.DatabaseManager.MySql.DatabaseObjects.Components;

namespace TcfOss.DatabaseManager.MySql.DefinitionMapping;

public partial class MyDiffer
{
    private MyConfig Config { get; }
    private readonly ILogger _logger;
    private readonly MyDefinition _start;
    private readonly MyDefinition _end;
    private readonly IEnumerable<Refactor> _refactors;
    private readonly IEnumerable<DeployScript> _deployScripts;

    private int Depth { get; }
    private readonly bool _includeSchema;

    private ImmutableHashSet<ForeignKeyInfo> _foreignKeys = [];
    private readonly HashSet<DroppedForeignKey> _droppedForeignKeys = [];

    private uint _nextRefactorWeight = DefaultWeights.ApplyRename;
    private uint _nextPreDeploymentScriptWeight = DefaultWeights.PreDeploymentScript;
    private uint _nextPostDropConstraintsScriptWeight = DefaultWeights.PostDropConstraintsScript;
    private uint _nextPreSetNotNullWeight = DefaultWeights.PreSetNotNullScript;
    private uint _nextPreAddConstraintsScriptWeight = DefaultWeights.PreAddConstraintsScript;
    private uint _nextPostDeploymentScriptWeight = DefaultWeights.PostDeploymentScript;
    private uint _nextAlterTableWeight = DefaultWeights.AlterTable;
    private uint _nextCreateProgramObjectWeight = DefaultWeights.CreateProgramObject;

    private readonly SqlValueList<DefinitionAlterStatement> _refactorStatements = [];
    private readonly Dictionary<SchemaIdentifier, List<string>> _appliedRefactors = [];

    private List<DefinitionAlterStatement>? _results;

    private readonly DifferFormatManager _manager;
    private readonly ValidationHelper _validationHelper;


    public MyDiffer(MyConfig config, MyDefinition start, MyDefinition end, IEnumerable<Refactor> refactors, IEnumerable<DeployScript> deployScripts, ILogger logger)
    {
        Config = config;
        _start = start.Copy();
        _end = end.Copy();
        _refactors = refactors;
        _deployScripts = deployScripts;
        _logger = logger;
        _includeSchema = config.DifferFormatting.ObjectNamePrefixWithSchema;
        Depth = _includeSchema ? 2 : 1;
        _manager = new DifferFormatManager
        {
            QuoteStyle = config.QuoteStyle,
            Formatting = config.DifferFormatting,
            AttributeDefaults = config.AttributeDefaults,
            Table = null,
        };
        _validationHelper = new ValidationHelper(logger);
    }

    public List<DefinitionAlterStatement> ComputeChanges()
    {
        if (_results == null)
        {
            _foreignKeys = GetForeignKeys(_start, _validationHelper, Config.NameHandling);
            ApplyRefactors();
            _foreignKeys = GetForeignKeys(_start, _validationHelper, Config.NameHandling);

            var results = new List<DefinitionAlterStatement>(_refactorStatements);

            results.AddRange(GetAllAlterTables());
            results.AddRange(GetProgramChanges());
            results.AddRange(GetTriggerChanges());
            results.AddRange(GetEventChanges());

            results.AddRange(GetDeployScriptStatements());

            results.AddRange(GetInsertRefactorStatements());
            results.AddRange(GetInsertDeployScriptStatements());

            _results = [.. results.OrderBy(x => x.Weight)];
        }
        return _results;
    }

    private List<DefinitionAlterStatement> GetDeployScriptStatements()
    {
        var results = new List<DefinitionAlterStatement>();
        foreach (DeployScript deployScript in _deployScripts.OrderBy(ds => ds.FileName, StringComparer.OrdinalIgnoreCase))
        {
            uint weight = deployScript.Type switch
            {
                DeployScriptType.PreDeployment => _nextPreDeploymentScriptWeight++,
                DeployScriptType.PostDropConstraints => _nextPostDropConstraintsScriptWeight++,
                DeployScriptType.PreSetNotNull => _nextPreSetNotNullWeight++,
                DeployScriptType.PreAddConstraints => _nextPreAddConstraintsScriptWeight++,
                DeployScriptType.PostDeployment => _nextPostDeploymentScriptWeight++,
                _ => throw new InvalidOperationException($"Unknown deploy script type '{deployScript.Type}'.")
            };
            var statement = new StatementGroup(deployScript.Body) { Meta = { RawText = deployScript.RawBodyText } };
            results.Add(new DefinitionAlterStatement(weight, deployScript.SchemaId, statement, $"Applying script from {deployScript.FullPath}", true));
        }
        return results;
    }

    private List<DefinitionAlterStatement> GetInsertRefactorStatements()
    {
        var results = new List<DefinitionAlterStatement>();
        var refactorTableId = new Identifier(StoredMetadataConstants.TableName, Config.QuoteStyle);

        foreach ((SchemaIdentifier schemaId, List<string> refactors) in _appliedRefactors)
        {
            IEnumerable<SqlValueList<Expression>> valueSets = refactors.Select(refactor =>
                new SqlValueList<Expression>(
                    [
                        new LiteralValue(new Value.SingleQuotedString(refactor)),
                        new LiteralValue(new Value.SingleQuotedString(StoredMetadataConstants.Refactor)),
                    ]
                )
            );

            var source = new Select(new SelectBody.ValuesQuery(new Values([.. valueSets])));
            var insertStatement = new Insert(new ObjectName([schemaId.ToSimpleIdentifier(), refactorTableId]), source)
            {
                Into = true,
                Columns = [new Identifier("entry_key", Config.QuoteStyle), new Identifier("entry_type", Config.QuoteStyle)]
            };

            results.Add(new DefinitionAlterStatement(DefaultWeights.InsertRefactor, schemaId, insertStatement));
        }

        return results;
    }

    private List<DefinitionAlterStatement> GetInsertDeployScriptStatements()
    {
        var results = new List<DefinitionAlterStatement>();
        var deployScriptTableId = new Identifier(StoredMetadataConstants.TableName, Config.QuoteStyle);

        var deployScriptsWithIds = _deployScripts.Where(ds => ds.UniqueId != null).ToList();

        IEnumerable<IGrouping<DeployScriptType, DeployScript>> deployScriptsByType = deployScriptsWithIds
            .GroupBy(ds => ds.Type)
            .OrderBy(g => g.Key);

        foreach (IGrouping<DeployScriptType, DeployScript> group in deployScriptsByType)
        {
            DeployScriptType type = group.Key;
            uint weight = type switch
            {
                DeployScriptType.PreDeployment => DefaultWeights.InsertPreDeployMeta,
                DeployScriptType.PostDropConstraints => DefaultWeights.InsertPostDropConstraintsMeta,
                DeployScriptType.PreSetNotNull => DefaultWeights.InsertPreSetNotNullMeta,
                DeployScriptType.PreAddConstraints => DefaultWeights.InsertPreAddConstraintsMeta,
                DeployScriptType.PostDeployment => DefaultWeights.InsertPostDeployMeta,
                _ => throw new InvalidOperationException($"Unknown deploy script type '{type}'.")
            };

            IEnumerable<IGrouping<SchemaIdentifier, DeployScript>> deployScriptsBySchema = group
                .GroupBy(ds => ds.SchemaId)
                .OrderBy(g => g.Key);

            foreach (IGrouping<SchemaIdentifier, DeployScript> schemaGroup in deployScriptsBySchema)
            {
                SchemaIdentifier schemaId = schemaGroup.Key;
                var deployScripts = schemaGroup.ToList();

                IEnumerable<SqlValueList<Expression>> valueSets = deployScripts.Select(ds =>
                    new SqlValueList<Expression>(
                        [
                            new LiteralValue(new Value.SingleQuotedString(ds.UniqueId!)),
                            new LiteralValue(new Value.SingleQuotedString(StoredMetadataConstants.DeployScript)),
                        ]
                    )
                );

                var source = new Select(
                    new SelectBody.ValuesQuery(new Values([.. valueSets]))
                );
                var insertStatement = new Insert(
                    new ObjectName([schemaId.ToSimpleIdentifier(), deployScriptTableId]),
                    source)
                {
                    Into = true,
                    Columns = [
                        new Identifier(StoredMetadataConstants.UidColumn, Config.QuoteStyle),
                        new Identifier(StoredMetadataConstants.TypeColumn, Config.QuoteStyle)
                    ]
                };

                results.Add(new DefinitionAlterStatement(weight, schemaId, insertStatement));
            }
        }

        return results;
    }

    private void ApplyRefactors()
    {
        foreach (Refactor refactor in _refactors)
        {
            switch (refactor)
            {
                case Refactor.TableRename rename:
                    ApplyTableRename(rename);
                    break;
                case Refactor.ColumnRename rename:
                    ApplyColumnRename(rename);
                    break;
                default:
                    throw new NotSupportedException($"Refactor type {refactor.GetType()} is not supported.");
            }
        }
        foreach (IGrouping<SchemaIdentifier, Refactor> x in _refactors.GroupBy(x => x.SchemaId))
        {
            if (!_appliedRefactors.TryGetValue(x.Key, out List<string>? schemaRefactors))
            {
                schemaRefactors = [];
                _appliedRefactors[x.Key] = schemaRefactors;
            }

            schemaRefactors.AddRange(x.Select(r => r.UniqueId));
        }
    }

    private void ApplyTableRename(Refactor.TableRename rename)
    {
        ObjectHandle oldHandle = ObjectHandle.Create(rename.OldName, Config.NameHandling);

        if (_start.Tables.TryGetValue(rename.OldName, out MyTable? table))
        {
            _start.Tables.Remove(rename.OldName);
            _start.Tables[rename.NewName] = RenameTable(table, rename.NewName);

            foreach (ForeignKeyInfo fkInfo in _foreignKeys.Where(fk => fk.RefTableHandle == oldHandle))
            {
                MyTable refTable = _start.Tables[fkInfo.TableHandle];
                _start.Tables[fkInfo.TableHandle] = RenameTableReferencingTable(refTable, rename.OldName, rename.NewName);
            }

            _refactorStatements.Add(
                new DefinitionAlterStatement(
                    _nextRefactorWeight,
                    rename.OldName.Schema,
                    new AlterTable(
                        rename.OldName.ToObjectName(Depth),
                        [
                            new AlterTableOperation.Rename(rename.OldName.Schema == rename.NewName.Schema ? rename.NewName.ToObjectName(Depth) : rename.NewName.ToObjectName(2))
                        ]
                    )
                )
            );
            _nextRefactorWeight++;
            _foreignKeys = GetForeignKeys(_start, _validationHelper, Config.NameHandling);
        }
        else
        {
            throw new InvalidOperationException($"Attempted to rename non-existing table: {rename.OldName}");
        }
    }

    private void ApplyColumnRename(Refactor.ColumnRename rename)
    {
        ObjectHandle tableHandle = ObjectHandle.Create(rename.OldName.Table, Config.NameHandling);

        if (_start.Tables.TryGetValue(tableHandle, out MyTable? table))
        {
            _start.Tables[tableHandle] = RenameColumnOwningTable(table, rename.OldName, rename.NewName);

            foreach (ForeignKeyInfo fkInfo in _foreignKeys.Where(fk => fk.RefTableHandle == tableHandle && fk.RefColumns.Contains(rename.OldName.ToSimpleIdentifier())))
            {
                MyTable refTable = _start.Tables[fkInfo.TableHandle];
                _start.Tables[fkInfo.TableHandle] = RenameColumnReferencingTable(refTable, rename.OldName, rename.NewName);
            }

            _refactorStatements.Add(
                new DefinitionAlterStatement(
                    _nextRefactorWeight,
                    rename.OldName.Table.Schema,
                    new AlterTable(
                        rename.OldName.Table.ToObjectName(Depth),
                        [
                            new AlterTableOperation.RenameColumn(rename.OldName.ToSimpleIdentifier(), rename.NewName.ToSimpleIdentifier())
                        ]
                    )
                )
            );
            _nextRefactorWeight++;
        }
        else
        {
            throw new InvalidOperationException($"Attempted to rename non-existing column: {rename.OldName}");
        }
    }

    private static MyTable RenameTable(MyTable table, ObjectIdentifier newName)
    {
        if (table.Name == newName)
        {
            return table;
        }
        var newCols = new SqlValueList<MyColumn>(table.Columns.Select(col => col with { Name = col.Name with { Table = newName } }));
        return table with { Name = newName, Columns = newCols };
    }

    private static MyTable RenameColumnOwningTable(MyTable table, ColumnIdentifier oldName, ColumnIdentifier newName)
    {
        int colIndex = table.Columns.FindIndex(x => x.Name == oldName);
        if (colIndex < 0)
        {
            throw new InvalidOperationException($"Attempted to rename non-existing column: {oldName}");
        }
        var newCols = new SqlValueList<MyColumn>(table.Columns.Select((col, i) => i == colIndex ? col with { Name = newName } : col));

        Identifier oldId = oldName.ToSimpleIdentifier();
        Identifier newId = newName.ToSimpleIdentifier();

        MyPrimaryKey? primaryKey = null;
        if (table.PrimaryKey != null)
        {
            primaryKey = table.PrimaryKey with
            {
                Columns = GetNewKeyParts(table.PrimaryKey.Columns, oldId, newId)
            };
        }


        var newUniqueKeys = new DatabaseComponentDict<MyUniqueKey>();
        foreach ((Handle keyId, MyUniqueKey key) in table.UniqueKeys)
        {
            if (key.Columns.Any(part => part is KeyPart.Column col && col.Name == oldId))
            {
                newUniqueKeys[keyId] = key with
                {
                    Columns = GetNewKeyParts(key.Columns, oldId, newId)
                };
            }
            else
            {
                newUniqueKeys[keyId] = key;
            }
        }

        var newForeignKeys = new DatabaseComponentDict<MyForeignKey>();
        foreach ((Handle fkId, MyForeignKey fk) in table.ForeignKeys)
        {
            if (fk.Columns.Contains(oldId))
            {
                newForeignKeys[fkId] = fk with
                {
                    Columns = [.. fk.Columns.Select(col => col == oldId ? newId : col)],
                };
            }
            else
            {
                newForeignKeys[fkId] = fk;
            }
        }

        var newKeys = new DatabaseComponentDict<MyKey>();
        foreach ((Handle keyId, MyKey key) in table.Keys)
        {
            if (key.Columns.Any(part => part is KeyPart.Column col && col.Name == oldId))
            {
                newKeys[keyId] = key with
                {
                    Columns = GetNewKeyParts(key.Columns, oldId, newId)
                };
            }
            else
            {
                newKeys[keyId] = key;
            }
        }

        return table with
        {
            Columns = newCols,
            PrimaryKey = primaryKey,
            UniqueKeys = newUniqueKeys,
            ForeignKeys = newForeignKeys,
            Keys = newKeys
        };
    }

    private static MyTable RenameTableReferencingTable(MyTable table, ObjectIdentifier oldName, ObjectIdentifier newName)
    {
        var newForeignKeys = new DatabaseComponentDict<MyForeignKey>();
        foreach ((Handle fkId, MyForeignKey fk) in table.ForeignKeys)
        {
            if (fk.ReferencedTable == oldName)
            {
                newForeignKeys[fkId] = fk with
                {
                    ReferencedTable = newName,
                };
            }
            else
            {
                newForeignKeys[fkId] = fk;
            }
        }

        return table with
        {
            ForeignKeys = newForeignKeys
        };
    }

    private static MyTable RenameColumnReferencingTable(MyTable table, ColumnIdentifier oldName, ColumnIdentifier newName)
    {
        Identifier oldId = oldName.ToSimpleIdentifier();
        Identifier newId = newName.ToSimpleIdentifier();

        var newForeignKeys = new DatabaseComponentDict<MyForeignKey>();
        foreach ((Handle fkId, MyForeignKey fk) in table.ForeignKeys)
        {
            if (fk.ReferencedTable == oldName.Table && fk.ReferencedColumns.Contains(oldId))
            {
                newForeignKeys[fkId] = fk with
                {
                    ReferencedColumns = [.. fk.ReferencedColumns.Select(col => col == oldId ? newId : col)],
                };
            }
            else
            {
                newForeignKeys[fkId] = fk;
            }
        }

        return table with
        {
            ForeignKeys = newForeignKeys
        };
    }

    private static SqlValueList<KeyPart> GetNewKeyParts(IEnumerable<KeyPart> keyParts, Identifier oldId, Identifier newId)
    {
        var newKeyParts = new SqlValueList<KeyPart>();
        foreach (KeyPart keyPart in keyParts)
        {
            if (keyPart is KeyPart.Column col && col.Name == oldId)
            {
                newKeyParts.Add(col with { Name = newId });
            }
            else
            {
                newKeyParts.Add(keyPart);
            }
        }
        return newKeyParts;
    }

    private List<DefinitionAlterStatement> GetAllAlterTables()
    {
        var results = new List<DefinitionAlterStatement>();
        List<ObjectKeyMapping<MyTable>> tableMaps = MyObjectMapper.GetMappings(_start.Tables, _end.Tables, excludeEquals: true);

        foreach (ObjectKeyMapping<MyTable> tableMap in tableMaps)
        {
            if (tableMap.Start == null)
            {
                results.Add(
                    new DefinitionAlterStatement(
                        DefaultWeights.CreateTable,
                        tableMap.End!.Name.Schema,
                        tableMap.End!.ToCreateStatement(
                            _includeSchema,
                            _manager with
                            {
                                OmitForeignKeysOnCreateTable = true,
                                Table = tableMap.End!
                            }
                        )));
            }
            else if (tableMap.End == null)
            {
                results.Add(
                    new DefinitionAlterStatement(
                        DefaultWeights.DropTable,
                        tableMap.Start!.Name.Schema,
                        new DropObject([tableMap.Start!.Name.ToObjectName(Depth)], DroppableObject.Table)
                    )
                );
            }
            else
            {
                if (tableMap.Start!.CharacterSet != tableMap.End!.CharacterSet || tableMap.Start!.Collation != tableMap.End!.Collation)
                {
                    results.Add(
                        new DefinitionAlterStatement(
                            DefaultWeights.SetCharacterSetCollation,
                            tableMap.Start!.Name.Schema,
                            new AlterTable(tableMap.End.Name.ToObjectName(Depth), [new AlterTableOperation.SetCharacterSetCollation(tableMap.End!.CharacterSet, tableMap.End!.Collation)])
                        )
                    );
                }
                results.AddRange(GetAlterTableColumns(tableMap.Handle, tableMap.Start!, tableMap.End!));
            }
        }

        var neverExclude = new HashSet<ObjectHandle>(_droppedForeignKeys.Select(x => x.TableHandle));
        tableMaps = MyObjectMapper.GetMappings(_start.Tables, _end.Tables, out HashSet<ObjectHandle> excluded, excludeEquals: true, neverExclude: neverExclude);

        List<KeyDiffer> keyDiffers = [];

        foreach (ObjectKeyMapping<MyTable> tableMap in tableMaps)
        {
            if (tableMap.Start == null && tableMap.End == null)
            {
                continue;
            }
            var keyDiffer = new KeyDiffer(this, tableMap.Handle, tableMap.Name, tableMap.Start, tableMap.End);
            keyDiffer.ProcessAllExceptForeignKeys();
            keyDiffers.Add(keyDiffer);
        }

        foreach (ObjectHandle excludedTable in excluded)
        {
            if (_droppedForeignKeys.Any(fk => fk.TableHandle == excludedTable))
            {
                MyTable? startTable = _start.Tables.GetValueOrDefault(excludedTable);
                MyTable? endTable = _end.Tables.GetValueOrDefault(excludedTable);
                ObjectIdentifier tableId = startTable != null ? startTable.Name : endTable!.Name;
                keyDiffers.Add(new KeyDiffer(this, excludedTable, tableId, startTable, endTable));
            }
        }

        foreach (KeyDiffer keyDiffer in keyDiffers)
        {
            keyDiffer.ProcessForeignKeys();
            results.AddRange(keyDiffer.Statements);
        }

        return results;
    }

    private List<DefinitionAlterStatement> GetAlterTableColumns(ObjectHandle tableHandle, MyTable start, MyTable end)
    {
        var tableDiffer = new ColumnDiffer(this, tableHandle, start, end, _nextAlterTableWeight);
        _nextAlterTableWeight = tableDiffer.MyOrder;
        return tableDiffer.Statements;
    }

    private List<DefinitionAlterStatement> GetProgramChanges()
    {
        var results = new List<DefinitionAlterStatement>();
        var unsortedObjectsToCreate = new List<IDatabaseObject>();
        List<ObjectKeyMapping<MyStoredProcedure>> procedureMaps = MyObjectMapper.GetMappings(_start.Procedures, _end.Procedures, excludeEquals: true);
        foreach (ObjectKeyMapping<MyStoredProcedure> map in procedureMaps)
        {
            if (map.Start != null)
            {
                results.Add(new DefinitionAlterStatement(DefaultWeights.DropProgramObject, map.Start.Name.Schema, new DropObject([map.Start.Name.ToObjectName(Depth)], DroppableObject.Procedure)));
            }
            if (map.End != null)
            {
                unsortedObjectsToCreate.Add(map.End);
            }
        }
        List<ObjectKeyMapping<MyStoredFunction>> functionMaps = MyObjectMapper.GetMappings(_start.Functions, _end.Functions, excludeEquals: true);
        foreach (ObjectKeyMapping<MyStoredFunction> map in functionMaps)
        {
            if (map.Start != null)
            {
                results.Add(new DefinitionAlterStatement(DefaultWeights.DropProgramObject, map.Start.Name.Schema, new DropObject([map.Start.Name.ToObjectName(Depth)], DroppableObject.Function)));
            }
            if (map.End != null)
            {
                unsortedObjectsToCreate.Add(map.End);
            }
        }
        List<ObjectKeyMapping<MyView>> viewMaps = MyObjectMapper.GetMappings(_start.Views, _end.Views, excludeEquals: true);
        foreach (ObjectKeyMapping<MyView> map in viewMaps)
        {
            if (map.Start != null)
            {
                results.Add(new DefinitionAlterStatement(DefaultWeights.DropView, map.Start.Name.Schema, new DropObject([map.Start.Name.ToObjectName(Depth)], DroppableObject.View), Comment: $"Drop view {map.Start.Name}"));
            }
            if (map.End != null)
            {
                unsortedObjectsToCreate.Add(map.End);
            }
        }

        var resolver = new DependencyResolver(_logger);
        Dictionary<ObjectHandle, DbObjectNode> nodeMap = resolver.ConstructNodeMap(unsortedObjectsToCreate, _end, _end.ToPseudoTableSet(_end.Tables.Values.FirstOrDefault()?.Name.Schema), new MyFunctionNameProvider(), _manager, Config.NameHandling);
        List<DbObjectNode> sortedNodes = resolver.TopologicalSort(nodeMap);
        foreach (DbObjectNode node in sortedNodes)
        {

            results.Add(new DefinitionAlterStatement(_nextCreateProgramObjectWeight, node.Name.Schema, node.CreateStatement, Comment: $"Create {node.ObjectType} {node.Name}"));
            _nextCreateProgramObjectWeight++;
        }

        return results;
    }

    private List<DefinitionAlterStatement> GetTriggerChanges()
    {
        var results = new List<DefinitionAlterStatement>();
        List<ObjectKeyMapping<MyTrigger>> triggerMaps = MyObjectMapper.GetMappings(_start.Triggers, _end.Triggers, excludeEquals: true);

        foreach (ObjectKeyMapping<MyTrigger> map in triggerMaps)
        {
            if (map.Start != null)
            {
                results.Add(new DefinitionAlterStatement(
                    DefaultWeights.DropProgramObject,
                    map.Start.Name.Schema,
                    new DropObject([map.Start.Name.ToObjectName(Depth)], DroppableObject.Trigger),
                    Comment: $"Drop trigger {map.Start.Name}"
                ));
            }
            if (map.End != null)
            {
                results.Add(new DefinitionAlterStatement(
                    DefaultWeights.CreateTrigger,
                    map.End.Name.Schema,
                    map.End.ToCreateStatement(_includeSchema),
                    Comment: $"Create trigger {map.End.Name}"
                ));
            }
        }

        return results;
    }

    private List<DefinitionAlterStatement> GetEventChanges()
    {
        var results = new List<DefinitionAlterStatement>();
        List<ObjectKeyMapping<MyEvent>> eventMaps = MyObjectMapper.GetMappings(_start.Events, _end.Events, excludeEquals: true);

        foreach (ObjectKeyMapping<MyEvent> map in eventMaps)
        {
            if (map.Start != null)
            {
                results.Add(new DefinitionAlterStatement(DefaultWeights.DropProgramObject, map.Start.Name.Schema, new DropObject([map.Start.Name.ToObjectName(Depth)], DroppableObject.Event)));
            }
            if (map.End != null)
            {
                results.Add(new DefinitionAlterStatement(DefaultWeights.CreateEvent, map.End.Name.Schema, map.End.ToCreateStatement(_includeSchema)));
            }
        }

        return results;
    }

    private bool NotifyDroppingForeignKey(ObjectHandle onTableHandle, ObjectIdentifier onTable, Identifier foreignKey)
    {
        return _droppedForeignKeys.Add(new DroppedForeignKey(onTableHandle, onTable, foreignKey));
    }

    private List<ForeignKeyInfo> GetReferencingForeignKeys(ObjectHandle tableHandle, Identifier column)
    {
        return [.. _foreignKeys.Where(fk => fk.RefTableHandle == tableHandle && fk.RefColumns.Contains(column))];
    }

    private bool WasForeignKeyDropped(ObjectHandle table, Identifier foreignKey)
    {
        return _droppedForeignKeys.Any(fk => fk.TableHandle == table && fk.ForeignKey == foreignKey);
    }

    private List<DroppedForeignKey> GetForeignKeysUsingIndex(ObjectHandle tableHandle, Identifier? index)
    {
        IEnumerable<DroppedForeignKey> localFks = _foreignKeys
            .Where(fk => fk.TableHandle == tableHandle && fk.BackingIndex.Name == index)
            .Select(fk => new DroppedForeignKey(fk.TableHandle, fk.TableId, fk.ForeignKey));
        IEnumerable<DroppedForeignKey> refFks = _foreignKeys
            .Where(fk => fk.RefTableHandle == tableHandle && fk.RefBackingIndex.Name == index)
            .Select(fk => new DroppedForeignKey(fk.TableHandle, fk.TableId, fk.ForeignKey));

        return [.. localFks, .. refFks];
    }

    private static ImmutableHashSet<ForeignKeyInfo> GetForeignKeys(MyDefinition def, ValidationHelper validationHelper, NameHandling nameHandling)
    {
        var fks = new HashSet<ForeignKeyInfo>();
        foreach ((ObjectHandle tableKey, MyTable table) in def.Tables)
        {
            foreach ((Handle fkName, MyForeignKey fk) in table.ForeignKeys)
            {
                NamedKeyPartList backingIndex = validationHelper.GetValidMatchingKeyPartList(fk.Columns, table.GetPotentialKeyPartLists(), $"Foreign key {fkName} on table {tableKey}")!.Value;
                NamedKeyPartList refBackingIndex = validationHelper.GetValidMatchingKeyPartList(fk.ReferencedColumns, def.Tables[fk.ReferencedTable].GetPotentialKeyPartLists(), $"Referenced table {fk.ReferencedTable} for foreign key {fkName} on table {tableKey}")!.Value;
                fks.Add(new ForeignKeyInfo(fk.ConstraintName!, tableKey, table.Name, fk.Columns, ObjectHandle.Create(fk.ReferencedTable, nameHandling), fk.ReferencedTable, fk.ReferencedColumns, backingIndex, refBackingIndex));
            }
        }
        return [.. fks];
    }

    public record struct ForeignKeyInfo(Identifier ForeignKey, ObjectHandle TableHandle, ObjectIdentifier TableId, SqlValueList<Identifier> Columns, ObjectHandle RefTableHandle, ObjectIdentifier RefTableId, SqlValueList<Identifier> RefColumns, NamedKeyPartList BackingIndex, NamedKeyPartList RefBackingIndex);
    public record struct DroppedForeignKey(ObjectHandle TableHandle, ObjectIdentifier TableId, Identifier ForeignKey);
}
