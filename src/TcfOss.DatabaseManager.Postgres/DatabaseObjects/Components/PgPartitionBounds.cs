using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

public abstract record PgPartitionBounds
{
    public abstract PgPartitionBound ToStatementBound();

    public record DefaultPartition : PgPartitionBounds
    {
        public override PgPartitionBound ToStatementBound()
        {
            return new PgPartitionBound.DefaultPartition();
        }
    }

    public record List(SqlValueList<Expression> Values) : PgPartitionBounds
    {
        public override PgPartitionBound ToStatementBound()
        {
            return new PgPartitionBound.ListValues([.. Values]);
        }
    }

    public record Range(SqlValueList<RangeValue> From, SqlValueList<RangeValue> To) : PgPartitionBounds
    {
        public override PgPartitionBound ToStatementBound()
        {
            return new PgPartitionBound.RangeValues(From.Map(value => value.ToStatementValue()), To.Map(value => value.ToStatementValue()));
        }
    }

    public record Hash(int Modulus, int Remainder) : PgPartitionBounds
    {
        public override PgPartitionBound ToStatementBound()
        {
            return new PgPartitionBound.HashValues(Modulus, Remainder);
        }
    }

    public abstract record RangeValue
    {
        public abstract PgPartitionBound.RangeValue ToStatementValue();

        public record MinValue : RangeValue
        {
            public override PgPartitionBound.RangeValue ToStatementValue()
            {
                return new PgPartitionBound.RangeValue.MinValue();
            }
        }

        public record MaxValue : RangeValue
        {
            public override PgPartitionBound.RangeValue ToStatementValue()
            {
                return new PgPartitionBound.RangeValue.MaxValue();
            }
        }

        public record ExpressionValue(Expression Expression) : RangeValue
        {
            public override PgPartitionBound.RangeValue ToStatementValue()
            {
                return new PgPartitionBound.RangeValue.ExpressionValue(Expression);
            }
        }
    }
}
