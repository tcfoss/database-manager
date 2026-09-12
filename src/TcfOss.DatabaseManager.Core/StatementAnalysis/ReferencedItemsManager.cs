using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Expressions;

namespace TcfOss.DatabaseManager.Core.StatementAnalysis;

public class ReferencedItemsManager
{
    private LinkedList<ReferencedItemsContext> ContextStack { get; } = [];
    private int _tableScopeDepth;

    public ObjectNameFilters Filters { get; init; }

    public IFunctionNameProvider? FunctionNameProvider { get; init; }
    public IHaveObjects? Definition { get; init; }
    public PseudoTableSet? PseudoTables { get; set; }
    public SchemaIdentifier? ActiveSchema { get; set; }
    public HashSet<Identifier> LocalVariables { get; } = [];
    public NameHandling NameHandling { get; init; }

    private static ObjectNameFilters TypeToFilter(ItemType type) => type switch
    {
        ItemType.Unknown => ObjectNameFilters.Unknown,
        ItemType.Table => ObjectNameFilters.Table,
        ItemType.TableColumn => ObjectNameFilters.TableColumn,
        ItemType.View => ObjectNameFilters.View,
        ItemType.ViewColumn => ObjectNameFilters.ViewColumn,
        ItemType.DerivedTable => ObjectNameFilters.DerivedTable,
        ItemType.DerivedColumn => ObjectNameFilters.DerivedColumn,
        ItemType.Function => ObjectNameFilters.Function,
        ItemType.Procedure => ObjectNameFilters.Procedure,
        ItemType.Variable => ObjectNameFilters.Variable,
        ItemType.Event => ObjectNameFilters.Event,
        ItemType.BuiltInFunction => ObjectNameFilters.BuiltInFunction,
        _ => ObjectNameFilters.None,
    };

    private static ItemType PseudoTableTypeToItemRefType(PseudoTableType type) => type switch
    {
        PseudoTableType.Table => ItemType.Table,
        PseudoTableType.View => ItemType.View,
        PseudoTableType.CommonTableExpression => ItemType.DerivedTable,
        PseudoTableType.DerivedTable => ItemType.DerivedTable,
        _ => ItemType.Unknown,
    };

    private static ItemType PseudoTableTypeToColumnItemRefType(PseudoTableType type) => type switch
    {
        PseudoTableType.Table => ItemType.TableColumn,
        PseudoTableType.View => ItemType.ViewColumn,
        PseudoTableType.CommonTableExpression => ItemType.DerivedColumn,
        PseudoTableType.DerivedTable => ItemType.DerivedColumn,
        _ => ItemType.Unknown,
    };

    private bool MaybeExpectingTableOrView()
    {
        ReferencedItemsContext? ctx = CurrentContext;
        return ctx == ReferencedItemsContext.FromClause
            || ctx == ReferencedItemsContext.InsertTarget
            || ctx == ReferencedItemsContext.UpdateClause
            || ctx == ReferencedItemsContext.AlterTableTarget
            || ctx == ReferencedItemsContext.DropObjectStatement
            || ctx == ReferencedItemsContext.TruncateTarget
            || ctx == ReferencedItemsContext.CreateTriggerTable;
    }

    // private bool MaybeExpectingAnyObject()
    // {
    //     ReferencedItemsContext? ctx = CurrentContext;
    //     return ctx == ReferencedItemsContext.DropObjectStatement;
    // }

    private bool IsLocalVariable(Identifier identifier)
    {
        return LocalVariables.Any(v => string.Equals(v.Name, identifier.Name, StringComparison.OrdinalIgnoreCase));
    }

    public void AddLocalVariable(Identifier identifier)
    {
        LocalVariables.Add(identifier);
    }

    private bool MaybeExpectingColumn()
    {
        ReferencedItemsContext? ctx = CurrentContext;
        return ctx == ReferencedItemsContext.SelectItem
            || ctx == ReferencedItemsContext.SelectValueList
            || ctx == ReferencedItemsContext.WhereClause
            || ctx == ReferencedItemsContext.JoinCondition
            || ctx == ReferencedItemsContext.GroupByClause
            || ctx == ReferencedItemsContext.OrderByClause
            || ctx == ReferencedItemsContext.HavingClause
            || ctx == ReferencedItemsContext.UpdateColumn
            || ctx == ReferencedItemsContext.UpdateValue
            || ctx == ReferencedItemsContext.InsertColumnList
            || ctx == ReferencedItemsContext.InsertValueList;
    }

    private ItemType DetermineType(SqlValueList<Identifier> identifiers, out ObjectHandle? objectHandle, out ColumnIdentifier? columnIdentifier)
    {
        objectHandle = null;
        columnIdentifier = null;

        if (MaybeExpectingTableOrView())
        {
            if (PseudoTables != null)
            {
                PseudoTable[] matches = PseudoTables.GetPseudoTables(
                    tableName: identifiers.Last().Name,
                    schemaName: identifiers.Count >= 2 ? identifiers[^2].Name : null,
                    catalogName: identifiers.Count == 3 ? identifiers[0].Name : null);

                if (matches.Length > 0)
                {
                    if (matches[0].Identifier != null)
                    {
                        objectHandle = ObjectHandle.Create(matches[0].Identifier!, NameHandling);
                    }
                    else if (ActiveSchema != null)
                    {
                        objectHandle = ObjectHandle.Create(identifiers, ActiveSchema, NameHandling);
                    }
                    return PseudoTableTypeToItemRefType(matches[0].Type);
                }
            }

            if (Definition != null && ActiveSchema != null)
            {
                ObjectType? objectType = Definition.GetObjectType(identifiers, ActiveSchema, NameHandling);
                if (objectType != null)
                {
                    objectHandle = ObjectHandle.Create(identifiers, ActiveSchema, NameHandling);
                    return objectType == ObjectType.View ? ItemType.View : ItemType.Table;
                }
            }

            return ItemType.Unknown;
        }

        if (MaybeExpectingColumn())
        {
            if (PseudoTables != null && identifiers.Count >= 1)
            {
                PseudoTableSet.SelectableItem[] items;
                if (identifiers.Count >= 2)
                {
                    items = PseudoTables.GetSelectableElements(
                        identifiers.Last().Name,
                        identifiers[^2].Name,
                        identifiers.Count == 3 ? identifiers[0].Name : null);
                }
                else
                {
                    items = PseudoTables.GetSelectableElements(identifiers[0].Name);
                }

                if (items.Length == 1)
                {
                    ItemType colType = PseudoTableTypeToColumnItemRefType(items[0].SourceType);

                    if (colType != ItemType.Unknown && items[0].SourceIdentifier != null)
                    {
                        ObjectIdentifier tableIdentifier = items[0].SourceIdentifier!;
                        columnIdentifier = new ColumnIdentifier(identifiers.Last().Name, tableIdentifier);
                        objectHandle = ObjectHandle.Create(tableIdentifier, NameHandling);
                    }

                    return colType;
                }
            }

            if (identifiers.Count == 1 && (IsLocalVariable(identifiers[0]) || identifiers[0].Sigil == SigilKind.Variable))
            {
                return ItemType.Variable;
            }

            return ItemType.Unknown;
        }

        if (identifiers is [{ Sigil: SigilKind.Variable }])
        {
            return ItemType.Variable;
        }

        return ItemType.Unknown;
    }

    public ItemRef? CreateItemRef(SingleIdentifier identifier)
    {
        SqlValueList<Identifier> identifiers = [identifier.Identifier];
        ItemType type = DetermineType(identifiers, out ObjectHandle? objectHandle, out _);
        if (!Filters.HasFlag(TypeToFilter(type)))
        {
            return null;
        }

        return new ItemRef.SingleIdentifierRef(type, identifier) { ObjectHandle = objectHandle };
    }

    public ItemRef? CreateItemRef(CompoundIdentifier identifier)
    {
        ItemType type = DetermineType(identifier.Identifiers, out ObjectHandle? objectHandle, out ColumnIdentifier? columnIdentifier);
        if (!Filters.HasFlag(TypeToFilter(type)))
        {
            return null;
        }

        return new ItemRef.CompoundIdentifierRef(type, identifier)
        {
            ObjectHandle = objectHandle,
            ColumnIdentifier = columnIdentifier,
        };
    }

    public ItemRef? CreateObjectRef(ObjectName name)
    {
        ItemType type = DetermineType(name.Values, out ObjectHandle? objectHandle, out _);
        if (!Filters.HasFlag(TypeToFilter(type)))
        {
            return null;
        }

        return new ItemRef.ObjectNameRef(type, name) { ObjectHandle = objectHandle };
    }

    public ItemRef? CreateFunctionOrProcRef(ObjectName name)
    {
        ItemType type;
        ObjectHandle? objectHandle = null;

        if (name.Values.Count == 1 && FunctionNameProvider != null && FunctionNameProvider.IsBuiltInFunction(name.Values[0].Name))
        {
            type = ItemType.BuiltInFunction;
        }
        else if (Definition != null && ActiveSchema != null)
        {
            ObjectType? objectType = Definition.GetObjectType(name.Values, ActiveSchema, NameHandling);
            if (objectType == ObjectType.Function)
            {
                objectHandle = ObjectHandle.Create(name.Values, ActiveSchema, NameHandling);
                type = ItemType.Function;
            }
            else if (objectType == ObjectType.Procedure)
            {
                objectHandle = ObjectHandle.Create(name.Values, ActiveSchema, NameHandling);
                type = ItemType.Procedure;
            }
            else
            {
                type = ItemType.Unknown;
            }
        }
        else
        {
            type = ItemType.Unknown;
        }

        if (!Filters.HasFlag(TypeToFilter(type)))
        {
            return null;
        }

        return new ItemRef.ObjectNameRef(type, name) { ObjectHandle = objectHandle };
    }

    public ContextScope Enter(ReferencedItemsContext context)
    {
        return new ContextScope(this, context);
    }

    public TableScope EnterTableScope()
    {
        return new TableScope(this);
    }

    public ReferencedItemsContext? CurrentContext => ContextStack.First?.Value;

    public readonly struct ContextScope : IDisposable
    {
        private readonly ReferencedItemsManager _manager;

        public ContextScope(ReferencedItemsManager manager, ReferencedItemsContext context)
        {
            _manager = manager;
            _manager.ContextStack.AddFirst(context);
        }

        public void Dispose()
        {
            _manager.ContextStack.RemoveFirst();
        }
    }

    public readonly struct TableScope : IDisposable
    {
        private readonly ReferencedItemsManager _manager;
        private readonly PseudoTableSet? _savedPseudoTables;

        public TableScope(ReferencedItemsManager manager)
        {
            _manager = manager;
            _savedPseudoTables = manager.PseudoTables;

            if (manager.PseudoTables != null)
            {
                manager.PseudoTables = manager._tableScopeDepth == 0
                    ? manager.PseudoTables.CloneExternalOnly()
                    : manager.PseudoTables.Clone();
                manager.PseudoTables.EnterSelectScope();
            }

            manager._tableScopeDepth++;
        }

        public void Dispose()
        {
            _manager._tableScopeDepth--;
            _manager.PseudoTables?.LeaveSelectScope();
            _manager.PseudoTables = _savedPseudoTables;
        }
    }
}
