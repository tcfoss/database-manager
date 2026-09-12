using System.Diagnostics;
using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DefinitionMapping;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;
using TcfOss.DatabaseManager.MySql.DatabaseObjects.Components;

namespace TcfOss.DatabaseManager.MySql.DefinitionMapping;

public partial class MyDiffer
{
    sealed class ColumnDiffer
    {
        public uint MyOrder { get; private set; }

        private List<AlterTableOperation> _currentModifications = [];
        private readonly MyDiffer _parent;
        private readonly ObjectHandle _tableHandle;
        private readonly MyTable _start;
        private readonly MyTable _end;
        private readonly ObjectName _tableName;

        public List<DefinitionAlterStatement> Statements { get; } = [];

        private readonly DifferFormatManager _manager;

        public ColumnDiffer(MyDiffer parent, ObjectHandle tableHandle, MyTable start, MyTable end, uint initialOrder)
        {
            MyOrder = initialOrder;
            _parent = parent;
            _tableHandle = tableHandle;
            _start = start;
            _end = end;
            _tableName = start.Name.ToObjectName(parent.Depth);
            _manager = new DifferFormatManager
            {
                QuoteStyle = _parent.Config.QuoteStyle,
                Formatting = _parent.Config.DifferFormatting,
                AttributeDefaults = _parent.Config.AttributeDefaults,
                Table = _end,
            };

            ComputeColumnDifferences();
        }

        private void ComputeColumnDifferences()
        {
            _currentModifications = [];
            foreach (MyObjectMapper.ColumnMapping mappingSet in MyObjectMapper.GetColumnMappingSets(_start.Columns, _end.Columns))
            {
                ApplyColumnChangeResults(mappingSet);
            }

            if (_currentModifications.Count != 0)
            {
                Statements.Add(new DefinitionAlterStatement(
                    MyOrder,
                    _start.Name.Schema,
                    new AlterTable(_tableName, [.. _currentModifications]),
                    $"Applying column modifications to table {_tableName}"
                ));
                MyOrder++;
                _currentModifications = [];
            }
        }

        private void ApplyColumnChangeResults(MyObjectMapper.ColumnMapping mappingSet)
        {
            (MyColumn? start, MyColumn? end, string? endPrev, MyObjectMapper.ColumnChangeType changeType, bool moved) = mappingSet;

            if (changeType == MyObjectMapper.ColumnChangeType.Dropped)
            {
                /* Dropping a column */
                ApplyDropColumn(start!);
                return;
            }

            if (changeType == MyObjectMapper.ColumnChangeType.Added)
            {
                /* Adding a column */
                ApplyAddColumn(end!, endPrev);
                return;
            }

            Debug.Assert(start != null && end != null, "Both start and end columns should be non-null here");
            Debug.Assert(start != end || moved, "Either the columns or their positions should differ here");

            ColumnChangeType colChanges = MyColumnComparer.GetColumnChanges(start, end, moved, changesRequiredHere: true);

            (colChanges, end) = ApplyAutoIncrement(start, end, colChanges);
            if (colChanges == ColumnChangeType.None)
            {
                return;
            }

            if (CouldChangesAffectForeignKeys(colChanges))
            {
                ProcessChangedForeignKeyColumns(start);
            }

            if (colChanges.HasFlag(ColumnChangeType.DropGeneration))
            {
                // This will drop and recreate the column. All other changes will be contained in
                // that operation.
                _ = ApplyDropGeneration(start, end, endPrev == null ? new AlterTableColumnPosition.First() : new AlterTableColumnPosition.AfterColumn(new Identifier(endPrev, _parent.Config.QuoteStyle)), colChanges);
                return;
            }

            // For the default checks, we use equality rather than HasFlag, because if there are
            // multiple changes, the catch-all ModifyColumn will handle them.
            if (colChanges == ColumnChangeType.DropDefault)
            {
                _ = ApplyDropDefault(end, colChanges);
                return;
            }
            if (colChanges == ColumnChangeType.SetDefault)
            {
                _ = ApplySetDefault(end, colChanges);
                return;
            }

            AlterTableColumnPosition? newPos = null;
            if (colChanges.HasFlag(ColumnChangeType.Order))
            {
                newPos = endPrev == null ? new AlterTableColumnPosition.First() : new AlterTableColumnPosition.AfterColumn(new Identifier(endPrev, _parent.Config.QuoteStyle));
            }

            _currentModifications.Add(new AlterTableOperation.ModifyColumn(end.ToStatementColumn(_manager)) { Position = newPos });
        }

        private void ApplyDropColumn(MyColumn start)
        {
            if (start.AutoIncrement)
            {
                var statementCol = (start with { AutoIncrement = false }).ToStatementColumn(_manager);
                Statements.Add(new DefinitionAlterStatement(
                    DefaultWeights.DropAutoIncrement,
                    _start.Name.Schema,
                    new AlterTable(_tableName, [new AlterTableOperation.ModifyColumn(statementCol)]),
                    $"Dropping auto-increment from column {start.Name}"
                ));
            }
            _currentModifications.Add(new AlterTableOperation.DropColumn(start.Name.ToSimpleIdentifier()));
        }

        private void ApplyAddColumn(MyColumn end, string? previousColumnName)
        {
            AlterTableColumnPosition addPos = previousColumnName == null ? new AlterTableColumnPosition.First() : new AlterTableColumnPosition.AfterColumn(new Identifier(previousColumnName, _parent.Config.QuoteStyle));

            if (end.AutoIncrement)
            {
                /* Separate adding the column from applying the AUTO_INCREMENT */
                Statements.Add(new DefinitionAlterStatement(
                     DefaultWeights.AddAutoIncrement,
                     _start.Name.Schema,
                     new AlterTable(_tableName, [new AlterTableOperation.ModifyColumn(end.ToStatementColumn(_manager))]),
                     $"Adding auto-increment to column {end.Name}"
                 ));
                end = end with { AutoIncrement = false };
            }

            StatementColumn statementColumn = end.ToStatementColumn(_manager);
            _currentModifications.Add(new AlterTableOperation.AddColumn(statementColumn) { Position = addPos });
        }

        private ColumnChangeType ApplyDropGeneration(MyColumn start, MyColumn end, AlterTableColumnPosition newColumnPosition, ColumnChangeType columnChanges)
        {
            string tempColName = Guid.NewGuid().ToString().Replace("-", "_");
            var tempColId = new Identifier(tempColName, _parent.Config.QuoteStyle);

            var statements = new SqlValueList<Statement>
            {
                new AlterTable(_tableName, [new AlterTableOperation.RenameColumn(start.Name.ToSimpleIdentifier(), tempColId)]),
                new AlterTable(_tableName, [new AlterTableOperation.AddColumn(end.ToStatementColumn(_manager)) { Position = newColumnPosition }]),
                new Update(new TableWithJoins(new TableFactor.Table(_tableName)), [new(new AssignmentTarget.ObjectName(new ObjectName(end.Name.ToSimpleIdentifier(_parent.Config.QuoteStyle))), new SingleIdentifier(tempColId))]),
                new AlterTable(_tableName, [new AlterTableOperation.DropColumn(tempColId)])
            };

            if (_currentModifications.Count != 0)
            {
                Statements.Add(new DefinitionAlterStatement(
                    MyOrder,
                    _start.Name.Schema,
                    new AlterTable(_tableName, [.. _currentModifications]),
                    $"Applying pending modifications before dropping generation for column {start.Name}"
                ));
                MyOrder++;
                _currentModifications = [];
            }

            Statements.Add(new DefinitionAlterStatement(
                MyOrder,
                _start.Name.Schema,
                new StatementGroup(statements),
                $"Dropping generation from column {start.Name} by recreating as {end.Name}"
            ));
            MyOrder++;
            columnChanges &= ~ColumnChangeType.DropGeneration;

            return columnChanges;
        }

        private (ColumnChangeType, MyColumn) ApplyAutoIncrement(MyColumn start, MyColumn end, ColumnChangeType colChanges)
        {
            if (colChanges.HasFlag(ColumnChangeType.AddAutoIncrement))
            {
                (colChanges, end) = ApplyAddAutoIncrement(end, colChanges);
            }
            if (colChanges.HasFlag(ColumnChangeType.DropAutoIncrement))
            {
                colChanges = ApplyRemoveAutoIncrement(start, colChanges);
            }

            return (colChanges, end);
        }

        private (ColumnChangeType, MyColumn) ApplyAddAutoIncrement(MyColumn end, ColumnChangeType colChanges)
        {
            Statements.Add(new DefinitionAlterStatement(
                DefaultWeights.AddAutoIncrement,
                _start.Name.Schema,
                new AlterTable(_tableName, [new AlterTableOperation.ModifyColumn(end.ToStatementColumn(_manager))]),
                $"Adding auto-increment to column {end.Name}"
            ));
            colChanges &= ~ColumnChangeType.AddAutoIncrement;
            return (colChanges, end with { AutoIncrement = false });
        }

        private ColumnChangeType ApplyRemoveAutoIncrement(MyColumn start, ColumnChangeType colChanges)
        {
            var startWithoutAi = (start with { AutoIncrement = false }).ToStatementColumn(_manager);
            Statements.Add(new DefinitionAlterStatement(
                DefaultWeights.DropAutoIncrement,
                _start.Name.Schema,
                new AlterTable(_tableName, [new AlterTableOperation.ModifyColumn(startWithoutAi)]),
                $"Dropping auto-increment from column {start.Name}"
            ));
            colChanges &= ~ColumnChangeType.DropAutoIncrement;
            return colChanges;
        }

        private ColumnChangeType ApplyDropDefault(MyColumn end, ColumnChangeType colChanges)
        {
            _currentModifications.Add(new AlterTableOperation.DropDefault(end.Name.ToSimpleIdentifier()));
            colChanges &= ~ColumnChangeType.DropDefault;
            return colChanges;
        }

        private ColumnChangeType ApplySetDefault(MyColumn end, ColumnChangeType colChanges)
        {
            StatementColumnOption.Default newDefault = end.Default switch
            {
                ColumnOption.ColumnDefault.DefaultValue dv => new StatementColumnOption.Default.DefaultValue(dv.Value),
                ColumnOption.ColumnDefault.DefaultExpression de => new StatementColumnOption.Default.DefaultExpression(de.Expression),
                _ => throw new UnreachableException()
            };

            _currentModifications.Add(new AlterTableOperation.SetDefault(end.Name.ToSimpleIdentifier(), newDefault));
            colChanges &= ~ColumnChangeType.SetDefault;
            return colChanges;
        }

        private static bool CouldChangesAffectForeignKeys(ColumnChangeType colChanges)
        {
            return colChanges.HasFlag(ColumnChangeType.DataType)
                        || colChanges.HasFlag(ColumnChangeType.DropGeneration)
                        || colChanges.HasFlag(ColumnChangeType.GenerationExpression)
                        || colChanges.HasFlag(ColumnChangeType.Nullability);
        }

        /// <summary>
        /// The change requires that foreign keys on the starting column be dropped and (possibly)
        /// recreated later. Signal to the table-differ that these foreign keys are being dropped.
        /// If the table-differ doesn't already know, add DROP FOREIGN KEY statements to the list.
        /// </summary>
        /// <param name="start"></param>
        private void ProcessChangedForeignKeyColumns(MyColumn start)
        {
            foreach (MyForeignKey localFk in _start.ForeignKeys.Values.Where(fk => fk.Columns.Contains(start.Name.ToSimpleIdentifier())))
            {
                if (_parent.NotifyDroppingForeignKey(_tableHandle, _start.Name, localFk.Name!))
                {
                    Statements.Add(new DefinitionAlterStatement(
                        DefaultWeights.DropForeignKey,
                        _start.Name.Schema,
                        new AlterTable(_tableName, [new AlterTableOperation.DropForeignKey(localFk.Name!)]),
                        $"Dropping foreign key {localFk.Name} due to column change {start.Name}"
                    ));
                }
            }

            foreach (ForeignKeyInfo refFk in _parent.GetReferencingForeignKeys(_tableHandle, start.Name.ToSimpleIdentifier()))
            {
                if (_parent.NotifyDroppingForeignKey(refFk.TableHandle, refFk.TableId, refFk.ForeignKey))
                {
                    Statements.Add(new DefinitionAlterStatement(
                        DefaultWeights.DropForeignKey,
                        refFk.TableId.Schema,
                        new AlterTable(refFk.TableId.ToObjectName(_parent.Depth), [new AlterTableOperation.DropForeignKey(refFk.ForeignKey)]),
                        $"Dropping referencing foreign key {refFk.ForeignKey} in table {refFk.TableHandle} due to column change {start.Name}"
                    ));
                }
            }
        }
    }
}
