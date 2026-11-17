using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;
using TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

namespace TcfOss.DatabaseManager.Postgres.DefinitionBuilding;

public class PgPartitionNormalizer(INormalizeComponents componentNormalizer, INormalizeSql sqlNormalizer)
{
    public PgPartitioning Normalize(PgPartitionBy partitionBy)
    {
        return new PgPartitioning(partitionBy.Strategy, partitionBy.Keys.Map(key => new PgPartitioning.Key(
            sqlNormalizer.NormalizeExpressionNoFlatten(key.Expression),
            componentNormalizer.NormalizeObjectNameNullable(key.OperatorClass))));
    }

    public PgPartitionBounds Normalize(PgPartitionBound bound, SourceRef? sourceRef)
    {
        return bound switch
        {
            PgPartitionBound.DefaultPartition => new PgPartitionBounds.DefaultPartition(),
            PgPartitionBound.ListValues list => new PgPartitionBounds.List(list.Values.Map(sqlNormalizer.NormalizeExpressionNoFlatten)),
            PgPartitionBound.RangeValues range => new PgPartitionBounds.Range(
                range.From.Map(value => NormalizeRangeValue(value, sourceRef)),
                range.To.Map(value => NormalizeRangeValue(value, sourceRef))),
            PgPartitionBound.HashValues hash => new PgPartitionBounds.Hash(hash.Modulus, hash.Remainder),
            _ => throw new SqlSyntaxException.UnsupportedDialectClause("PostgreSQL", "partition bound " + bound.GetType().Name, sourceRef)
        };
    }

    private PgPartitionBounds.RangeValue NormalizeRangeValue(PgPartitionBound.RangeValue value, SourceRef? sourceRef)
    {
        return value switch
        {
            PgPartitionBound.RangeValue.MinValue => new PgPartitionBounds.RangeValue.MinValue(),
            PgPartitionBound.RangeValue.MaxValue => new PgPartitionBounds.RangeValue.MaxValue(),
            PgPartitionBound.RangeValue.ExpressionValue expression => new PgPartitionBounds.RangeValue.ExpressionValue(
                sqlNormalizer.NormalizeExpressionNoFlatten(expression.Expression)),
            _ => throw new SqlSyntaxException.UnsupportedDialectClause("PostgreSQL", "partition range bound " + value.GetType().Name, sourceRef)
        };
    }
}
