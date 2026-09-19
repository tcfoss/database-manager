using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DefinitionMapping;
using TcfOss.DatabaseManager.MySql.DatabaseObjects.Components;

namespace TcfOss.DatabaseManager.MySql.DefinitionMapping;

public static class MyColumnComparer
{
    private static ColumnChangeType ApplyNullability(ColumnChangeType changes, MyColumn start, MyColumn end)
    {
        bool startIsNotNull = start.Nullability is ColumnOption.Nullability.NotNull;
        bool endIsNotNull = end.Nullability is ColumnOption.Nullability.NotNull;

        switch (startIsNotNull)
        {
            case true when !endIsNotNull:
                changes |= ColumnChangeType.SetNull;
                break;
            case false when endIsNotNull:
                changes |= ColumnChangeType.SetNotNull;
                break;
        }

        return changes;
    }

    private static ColumnChangeType ApplyDefaults(ColumnChangeType changes, MyColumn start, MyColumn end)
    {
        var explicitDefaultNull = new ColumnOption.ColumnDefault.DefaultValue(new Value.Null());

        ColumnOption.ColumnDefault startDefault = start.Default ?? explicitDefaultNull;
        ColumnOption.ColumnDefault endDefault = end.Default ?? explicitDefaultNull;

        if (startDefault != endDefault)
        {
            if (end.Default == null)
            {
                changes |= ColumnChangeType.DropDefault;
            }
            else
            {
                changes |= ColumnChangeType.SetDefault;
            }
        }

        return changes;
    }

    private static ColumnChangeType ApplyAutoIncrement(ColumnChangeType changes, MyColumn start, MyColumn end)
    {

        if (start.AutoIncrement && !end.AutoIncrement)
        {
            changes |= ColumnChangeType.DropAutoIncrement;
        }
        else if (!start.AutoIncrement && end.AutoIncrement)
        {
            changes |= ColumnChangeType.AddAutoIncrement;
        }

        return changes;
    }

    private static ColumnChangeType ApplyGeneration(ColumnChangeType changes, MyColumn start, MyColumn end)
    {
        if (start.Generated == null && end.Generated != null)
        {
            throw new InvalidOperationException("Cannot make a non-generated column a generated column");
        }
        else if (start.Generated != null && end.Generated == null)
        {
            changes |= ColumnChangeType.DropGeneration;
        }
        else if (start.Generated != null && end.Generated != null && start.Generated != end.Generated)
        {
            changes |= ColumnChangeType.GenerationExpression;
        }

        return changes;
    }

    public static ColumnChangeType GetColumnChanges(MyColumn start, MyColumn end, bool moved, bool changesRequiredHere = false)
    {
        ColumnChangeType changes = ColumnChangeType.None;

        if (moved)
        {
            changes |= ColumnChangeType.Order;
        }

        if (start.DataType != end.DataType)
        {
            changes |= ColumnChangeType.DataType;
        }

        changes = ApplyNullability(changes, start, end);

        changes = ApplyDefaults(changes, start, end);

        if (start.Check != end.Check)
        {
            changes |= ColumnChangeType.CheckConstraint;
        }

        changes = ApplyAutoIncrement(changes, start, end);

        if (start.Comment != end.Comment)
        {
            changes |= ColumnChangeType.Comment;
        }
        changes = ApplyGeneration(changes, start, end);

        if (changesRequiredHere && changes == ColumnChangeType.None)
        {
            throw new InvalidOperationException("Changes were required, but none were detected.");
        }

        return changes;
    }
}
