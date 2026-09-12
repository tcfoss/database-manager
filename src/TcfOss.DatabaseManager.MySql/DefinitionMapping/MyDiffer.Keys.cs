using System.Diagnostics;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DefinitionMapping;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;
using TcfOss.DatabaseManager.MySql.DatabaseObjects.Components;

namespace TcfOss.DatabaseManager.MySql.DefinitionMapping;

public partial class MyDiffer
{
    sealed class KeyDiffer
    {
        private readonly MyDiffer _parent;
        private readonly ObjectIdentifier _tableId;
        private readonly ObjectHandle _tableHandle;
        private readonly MyTable? _start;
        private readonly MyTable? _end;
        private readonly ObjectName _tableName;
        private readonly DifferFormatManager _manager;

        public List<DefinitionAlterStatement> Statements { get; } = [];
        private bool _computedNonForeignKeys;
        private bool _computedForeignKeys;

        public KeyDiffer(MyDiffer parent, ObjectHandle tableHandle, ObjectIdentifier tableId, MyTable? start, MyTable? end)
        {
            _parent = parent;
            _tableHandle = tableHandle;
            _tableId = tableId;
            _start = start;
            _end = end;

            if (start != null)
            {
                _tableName = start.Name.ToObjectName(parent.Depth);
                _manager = new DifferFormatManager
                {
                    QuoteStyle = parent.Config.QuoteStyle,
                    Formatting = parent.Config.DifferFormatting,
                    AttributeDefaults = parent.Config.AttributeDefaults,
                    Table = start,
                    OmitForeignKeysOnCreateTable = true // Foreign keys are handled separately
                };
            }
            else
            {
                _tableName = end!.Name.ToObjectName(parent.Depth);
                _manager = new DifferFormatManager
                {
                    QuoteStyle = parent.Config.QuoteStyle,
                    Formatting = parent.Config.DifferFormatting,
                    AttributeDefaults = parent.Config.AttributeDefaults,
                    Table = end,
                    OmitForeignKeysOnCreateTable = true // Foreign keys are handled separately
                };
            }
        }

        public void ProcessAllExceptForeignKeys()
        {
            if (!_computedNonForeignKeys)
            {
                // If creating a table, everything but the foreign keys are
                // added in the CREATE statement.
                if (_start != null)
                {
                    ComputePrimaryKeyDifferences(droppingTable: _end == null);
                    ComputeUniqueKeyDifferences();
                    ComputeKeyDifferences();
                    ComputeCheckDifferences();
                }
                _computedNonForeignKeys = true;
            }
        }

        public void ProcessForeignKeys()
        {
            if (!_computedForeignKeys)
            {
                ComputeForeignKeyDifferences();
                _computedForeignKeys = true;
            }
        }

        private void ComputePrimaryKeyDifferences(bool droppingTable = false)
        {
            MyPrimaryKey? startPrimaryKey = _start?.PrimaryKey;
            MyPrimaryKey? endPrimaryKey = _end?.PrimaryKey;

            if (startPrimaryKey == endPrimaryKey)
            {
                return; // No change in primary key
            }

            if (_end?.PrimaryKey != null)
            {
                // Primary key added
                Statements.Add(new DefinitionAlterStatement(
                    DefaultWeights.AddPrimaryKey,
                    _end!.Name.Schema,
                    new AlterTable(_tableName,
                    [
                        new AlterTableOperation.AddPrimaryKey(_end.PrimaryKey.ToStatementConstraint(_manager))
                    ]),
                    "Add primary key"));
            }
            if (_start?.PrimaryKey != null && !droppingTable)
            {
                // Primary key removed
                Statements.Add(new DefinitionAlterStatement(
                    DefaultWeights.DropPrimaryKey,
                    _start!.Name.Schema,
                    new AlterTable(_tableName,
                    [
                        new AlterTableOperation.DropPrimaryKey()
                    ]),
                    "Drop primary key"));

                HandleForeignKeyBackingIndexes(null);
            }
        }

        private void ComputeUniqueKeyDifferences()
        {
            List<HandleMapping<MyUniqueKey>> mappings = MyObjectMapper.GetKeyMappings(_start?.UniqueKeys ?? [], _end?.UniqueKeys ?? []);

            foreach ((Handle name, MyUniqueKey? startLoop, MyUniqueKey? endLoop) in mappings)
            {
                MyUniqueKey? start = startLoop;
                if (start != null && start.IndexMethod == null)
                {
                    start = start with { IndexMethod = _parent.Config.AttributeDefaults.IndexMethod };
                }

                MyUniqueKey? end = endLoop;
                if (end != null && end.IndexMethod == null)
                {
                    end = end with { IndexMethod = _parent.Config.AttributeDefaults.IndexMethod };
                }

                if (start == end)
                {
                    continue; // No change
                }

                if (end != null)
                {
                    // Unique key added or modified
                    Statements.Add(new DefinitionAlterStatement(
                        DefaultWeights.AddKey,
                        _end!.Name.Schema,
                        new AlterTable(_tableName,
                        [
                            new AlterTableOperation.AddUniqueKey(end.ToStatementConstraint(_manager))
                        ]),
                        $"Add or modify unique key {name}"));
                }
                if (start != null)
                {
                    // Unique key removed
                    Statements.Add(new DefinitionAlterStatement(
                        DefaultWeights.DropKey,
                        _start!.Name.Schema,
                        new AlterTable(_tableName,
                        [
                            new AlterTableOperation.DropUniqueKey(start.Name!)
                        ]),
                        $"Drop unique key {name}"));

                    HandleForeignKeyBackingIndexes(start.Name ?? start.Name!);
                }
            }
        }

        private void ComputeForeignKeyDifferences()
        {
            List<HandleMapping<MyForeignKey>> mappings = MyObjectMapper.GetKeyMappings(_start?.ForeignKeys ?? [], _end?.ForeignKeys ?? []);

            foreach ((Handle name, MyForeignKey? start, MyForeignKey? end) in mappings)
            {
                if (start == end)
                {
                    if (end != null && _parent.WasForeignKeyDropped(_tableHandle, end.Name!))
                    {
                        // Foreign key was dropped to allow modification
                        Statements.Add(new DefinitionAlterStatement(
                            DefaultWeights.AddForeignKey,
                            _end!.Name.Schema,
                            new AlterTable(_tableName,
                            [
                                new AlterTableOperation.AddForeignKey(
                                    end.ToStatementConstraint(
                                        _manager with
                                        {
                                            Table = _end,
                                        }))
                            ]),
                            $"Re-add foreign key {name}"));
                    }
                    continue; // No change
                }

                if (end != null)
                {
                    // Foreign key added or modified
                    Statements.Add(new DefinitionAlterStatement(
                        DefaultWeights.AddForeignKey,
                        _end!.Name.Schema,
                        new AlterTable(_tableName,
                        [
                            new AlterTableOperation.AddForeignKey(
                                end.ToStatementConstraint(
                                    _manager with
                                    {
                                        Table = _end,
                                    }))
                        ]),
                        $"Add or modify foreign key {name}"));
                }

                if (start != null)
                {
                    // Foreign key removed
                    if (_parent.NotifyDroppingForeignKey(_tableHandle, _tableId, start.Name!))
                    {
                        Statements.Add(new DefinitionAlterStatement(
                            DefaultWeights.DropForeignKey,
                            _start!.Name.Schema,
                            new AlterTable(_tableName,
                            [
                                new AlterTableOperation.DropForeignKey(start.Name!)
                            ]),
                            $"Drop foreign key {name}"));
                    }
                }
            }
        }

        private void ComputeKeyDifferences()
        {
            List<HandleMapping<MyKey>> mappings = MyObjectMapper.GetKeyMappings(_start?.Keys ?? [], _end?.Keys ?? []);

            foreach ((Handle name, MyKey? startLoop, MyKey? endLoop) in mappings)
            {
                MyKey? start = startLoop;
                if (start is MyKey.Standard stdStart && stdStart.IndexMethod == null)
                {
                    start = stdStart with { IndexMethod = _parent.Config.AttributeDefaults.IndexMethod };
                }
                MyKey? end = endLoop;
                if (end is MyKey.Standard stdEnd && stdEnd.IndexMethod == null)
                {
                    end = stdEnd with { IndexMethod = _parent.Config.AttributeDefaults.IndexMethod };
                }

                if (start == end)
                {
                    continue; // No change
                }

                if (end != null)
                {
                    AlterTableOperation operation = end switch
                    {
                        MyKey.Standard key => new AlterTableOperation.AddStandardKey(key.ToStatementConstraint(_manager)),
                        MyKey.FullText key => new AlterTableOperation.AddFullTextKey(key.ToStatementConstraint(_manager)),
                        MyKey.Spatial key => new AlterTableOperation.AddSpatialKey(key.ToStatementConstraint(_manager)),
                        _ => throw new UnreachableException($"Unknown key type: {end.GetType()}")
                    };

                    // Key added or modified
                    Statements.Add(new DefinitionAlterStatement(
                        DefaultWeights.AddKey,
                        _end!.Name.Schema,
                        new AlterTable(_tableName, [operation]),
                        $"Add or modify key {name}"));
                }
                if (start != null)
                {
                    // Key removed
                    Statements.Add(new DefinitionAlterStatement(
                        DefaultWeights.DropKey,
                        _start!.Name.Schema,
                        new AlterTable(_tableName,
                        [
                            new AlterTableOperation.DropKey(start.Name!)
                        ]),
                        $"Drop key {name}"));

                    HandleForeignKeyBackingIndexes(start.Name!);
                }
            }
        }

        private void HandleForeignKeyBackingIndexes(Identifier? indexName)
        {
            List<DroppedForeignKey> affectedFks = _parent.GetForeignKeysUsingIndex(_tableHandle, indexName);
            foreach (DroppedForeignKey fk in affectedFks)
            {
                // This will cause the foreign key to be dropped and re-added, which will update it to use the new index definition
                if (_parent.NotifyDroppingForeignKey(fk.TableHandle, fk.TableId, fk.ForeignKey))
                {
                    Statements.Add(new DefinitionAlterStatement(
                        DefaultWeights.DropForeignKey,
                        fk.TableId.Schema,
                        new AlterTable(fk.TableId.ToObjectName(_parent.Depth),
                        [
                            new AlterTableOperation.DropForeignKey(fk.ForeignKey)
                        ]),
                        $"Drop foreign key {fk.ForeignKey} on table {fk.TableHandle} due to index {indexName} modification"));
                }
            }
        }

        private void ComputeCheckDifferences()
        {
            List<HandleMapping<MyCheck>> mappings = MyObjectMapper.GetKeyMappings(_start?.Checks ?? [], _end?.Checks ?? []);

            foreach ((Handle name, MyCheck? startLoop, MyCheck? endLoop) in mappings)
            {
                MyCheck? start = startLoop;
                MyCheck? end = endLoop;

                if (start == end)
                {
                    continue; // No change
                }

                if (end != null)
                {
                    // Check constraint added or modified
                    Statements.Add(new DefinitionAlterStatement(
                        DefaultWeights.AddKey,
                        _end!.Name.Schema,
                        new AlterTable(_tableName,
                        [
                            new AlterTableOperation.AddCheck(end.ToStatementConstraint())
                        ]),
                        $"Add or modify check constraint {name}"));
                }
                if (start != null)
                {
                    // Check constraint removed
                    Statements.Add(new DefinitionAlterStatement(
                        DefaultWeights.DropKey,
                        _start!.Name.Schema,
                        new AlterTable(_tableName,
                        [
                            new AlterTableOperation.DropCheck(start.ConstraintName!)
                        ]),
                        $"Drop check constraint {name}"));
                }
            }
        }
    }
}
