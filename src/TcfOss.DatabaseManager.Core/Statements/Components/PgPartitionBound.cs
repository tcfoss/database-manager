using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using SqlExpression = TcfOss.DatabaseManager.Core.Expressions.Expression;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record PgPartitionBound : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);

    public virtual void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        ToSql(writer);
    }

    public IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        IEnumerable<SqlExpression> expressions = this switch
        {
            ListValues list => list.Values,
            RangeValues range => range.From.Concat(range.To).OfType<RangeValue.ExpressionValue>().Select(value => value.Expression),
            _ => []
        };
        return expressions.SelectMany(expression => expression.GetReferencedItems(context));
    }

    public record DefaultPartition : PgPartitionBound
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("DEFAULT");
        }
    }

    public record ListValues(SqlValueList<SqlExpression> Values) : PgPartitionBound
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"FOR VALUES IN ({Values})");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("FOR VALUES IN (");
            writer.FormatDelimited(Values, manager);
            writer.Write(")");
        }
    }

    public record RangeValues(SqlValueList<RangeValue> From, SqlValueList<RangeValue> To) : PgPartitionBound
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"FOR VALUES FROM ({From}) TO ({To})");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("FOR VALUES FROM (");
            writer.FormatDelimited(From, manager);
            writer.Write(") TO (");
            writer.FormatDelimited(To, manager);
            writer.Write(")");
        }
    }

    public record HashValues(int Modulus, int Remainder) : PgPartitionBound
    {
        public bool RemainderFirst { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            if (RemainderFirst)
            {
                writer.WriteSql($"FOR VALUES WITH (REMAINDER {Remainder}, MODULUS {Modulus})");
            }
            else
            {
                writer.WriteSql($"FOR VALUES WITH (MODULUS {Modulus}, REMAINDER {Remainder})");
            }
        }
    }

    public abstract record RangeValue : IWriteSql
    {
        public abstract void ToSql(SqlTextWriter writer);

        public virtual void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            ToSql(writer);
        }

        public record MinValue : RangeValue
        {
            public override void ToSql(SqlTextWriter writer)
            {
                writer.Write("MINVALUE");
            }
        }

        public record MaxValue : RangeValue
        {
            public override void ToSql(SqlTextWriter writer)
            {
                writer.Write("MAXVALUE");
            }
        }

        public record ExpressionValue(SqlExpression Expression) : RangeValue
        {
            public override void ToSql(SqlTextWriter writer)
            {
                Expression.ToSql(writer);
            }

            public override void FormatSql(SqlTextWriter writer, FormatManager manager)
            {
                Expression.FormatSql(writer, manager);
            }
        }
    }
}
