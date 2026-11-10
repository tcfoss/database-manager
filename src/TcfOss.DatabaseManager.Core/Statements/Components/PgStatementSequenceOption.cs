using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Attributes;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record PgStatementSequenceOption : IWriteSql
{
    public abstract string UniqueName { get; }
    public abstract void ToSql(SqlTextWriter writer);

    public virtual void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        ToSql(writer);
    }

    public record Start(long Value) : PgStatementSequenceOption
    {
        public bool UseWith { get; init; }
        public override string UniqueName => "START";

        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"START{(UseWith ? " WITH" : "")} {Value}");
        }
    }

    public record Increment(long Value) : PgStatementSequenceOption
    {
        public bool UseBy { get; init; }
        public override string UniqueName => "INCREMENT";

        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"INCREMENT{(UseBy ? " BY" : "")} {Value}");
        }
    }

    public record MinValue(long? Value) : PgStatementSequenceOption
    {
        public override string UniqueName => "MINVALUE";

        public override void ToSql(SqlTextWriter writer)
        {
            if (Value is { } minimum)
            {
                writer.WriteSql($"MINVALUE {minimum}");
            }
            else
            {
                writer.Write("NO MINVALUE");
            }
        }
    }

    public record MaxValue(long? Value) : PgStatementSequenceOption
    {
        public override string UniqueName => "MAXVALUE";

        public override void ToSql(SqlTextWriter writer)
        {
            if (Value is { } maximum)
            {
                writer.WriteSql($"MAXVALUE {maximum}");
            }
            else
            {
                writer.Write("NO MAXVALUE");
            }
        }
    }


    public record DataType(PgSequenceDataType Type) : PgStatementSequenceOption
    {
        public override string UniqueName => "AS";

        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"AS {Type}");
        }
    }

    public record OwnedBy(ObjectName? Column) : PgStatementSequenceOption
    {
        public override string UniqueName => "OWNED BY";

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("OWNED BY ");
            if (Column != null)
            {
                Column.ToSql(writer);
            }
            else
            {
                writer.Write("NONE");
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("OWNED BY ");
            if (Column != null)
            {
                Column.FormatSql(writer, manager);
            }
            else
            {
                writer.Write("NONE");
            }
        }

        public IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            if (Column != null)
            {
                using ReferencedItemsManager.ContextScope scope = context.Enter(ReferencedItemsContext.SequenceOwnedBy);
                var table = new ObjectName([.. Column.Values.SkipLast(1)]);
                ItemRef? item = context.CreateObjectRef(table);
                if (item != null)
                {
                    yield return item;
                }
            }
        }
    }

    public record Restart(long? Value) : PgStatementSequenceOption
    {
        public bool UseWith { get; init; }
        public override string UniqueName => "RESTART";

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("RESTART");
            if (Value != null)
            {
                writer.WriteSql($"{(UseWith ? " WITH" : "")} {Value.Value}");
            }
        }
    }

    public record Cache(long Value) : PgStatementSequenceOption
    {
        public override string UniqueName => "CACHE";

        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"CACHE {Value}");
        }
    }

    public record Cycle(bool Enabled) : PgStatementSequenceOption
    {
        public override string UniqueName => "CYCLE";

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write(Enabled ? "CYCLE" : "NO CYCLE");
        }
    }
}
