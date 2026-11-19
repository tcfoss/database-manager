using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;
using TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

namespace TcfOss.DatabaseManager.Postgres.DefinitionBuilding;

public class PgExclusionNormalizer(INormalizeComponents componentNormalizer, INormalizeSql sqlNormalizer)
{
    public PgTableConstraint.Exclusion Normalize(StatementTableConstraint.PgExclusion exclusion, Identifier name,
        IndexMethod method, Identifier? tablespace, SourceRef? sourceRef)
    {
        if (exclusion.Elements.Count == 0)
        {
            throw new SqlSyntaxException.EmptyIndexColumnList("An EXCLUDE constraint", sourceRef);
        }

        SqlValueList<PgTableConstraint.Exclusion.Element> elements = exclusion.Elements.Map(element => new PgTableConstraint.Exclusion.Element(
            NormalizeKey(element.Key, method, sourceRef),
            element.Operator with
            {
                Schema = componentNormalizer.NormalizeSingleIdentifierNullable(element.Operator.Schema),
                UseOperatorSyntax = element.Operator.Schema != null
            }));

        return new PgTableConstraint.Exclusion(elements, name)
        {
            Options = PgConstraintNormalizer.Normalize(exclusion.PgOptions, "EXCLUDE", name, sourceRef, allowDeferrable: true),
            Method = method,
            IncludedColumns = exclusion.IncludedColumns,
            Predicate = exclusion.Predicate == null ? null : sqlNormalizer.NormalizeExpressionNoFlatten(exclusion.Predicate),
            NormalizedPredicate = exclusion.Predicate == null ? null : sqlNormalizer.NormalizeExpression(exclusion.Predicate),
            BackingIndex = ApplyStorageParameters(new PgBackingIndex
            {
                Tablespace = tablespace
            }, exclusion.IndexParameters?.StorageParameters, name, sourceRef)
        };
    }

    private PgKeyPart NormalizeKey(StatementKeyPart key, IndexMethod method, SourceRef? sourceRef)
    {
        PgKeyPart normalized = key switch
        {
            StatementKeyPart.Column { Length: null } column => new PgKeyPart.Column(componentNormalizer.NormalizeSingleIdentifier(column.Name))
            {
                Direction = column.Direction
            },
            StatementKeyPart.IndexExpression expression => new PgKeyPart.IndexExpression(sqlNormalizer.NormalizeExpressionNoFlatten(expression.Expression))
            {
                Direction = expression.Direction,
                NormalizedExpression = sqlNormalizer.NormalizeExpression(expression.Expression)
            },
            _ => throw new SqlSyntaxException.UnsupportedDialectClause("PostgreSQL", "EXCLUDE index column prefix lengths", sourceRef)
        };

        Direction? direction = normalized.Direction;
        NullsOrder? nullsOrder = key.NullsOrder;
        if (IndexMethod.IsBtree(method))
        {
            if (nullsOrder == (direction == Direction.Descending ? NullsOrder.First : NullsOrder.Last))
            {
                nullsOrder = null;
            }
            if (direction == Direction.Ascending)
            {
                direction = null;
            }
        }
        return normalized with
        {
            Direction = direction,
            NullsOrder = nullsOrder,
            OperatorClass = NormalizeOperatorClass(key.OperatorClass, sourceRef)
        };
    }

    private PgKeyPart.OperatorClassOptions? NormalizeOperatorClass(PgStatementOperatorClass? options, SourceRef? sourceRef)
    {
        PgKeyPart.OperatorClassOptions? result = options?.Name is { } operatorClass
            ? new PgKeyPart.OperatorClassOptions(componentNormalizer.NormalizeObjectName(operatorClass)) : null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (ObjectNameKvp parameter in options?.Parameters ?? [])
        {
            ObjectName name = componentNormalizer.NormalizeObjectName(parameter.Name);
            if (result == null || name.Values.Count != 1
                || name.Values[0].Name is not ("siglen" or "numranges")
                || parameter.Value is not Value.Number { IsLong: false } number
                || number.AsInt() is not > 0)
            {
                throw new SqlSyntaxException.UnsupportedDialectClause("PostgreSQL", $"operator class parameter {parameter.ToSql()}", sourceRef);
            }
            if (!seen.Add(name.Values[0].Name))
            {
                throw new SqlSyntaxException.SpecifiedMoreThanOnce(name.Values[0].Name, "operator class", result.Name.Values[^1], sourceRef);
            }
            result = name.Values[0].Name == "siglen"
                ? result with { SignatureLength = number.AsInt() }
                : result with { NumberOfRanges = number.AsInt() };
        }
        return result;
    }

    private static PgBackingIndex ApplyStorageParameters(PgBackingIndex index, SqlValueList<StatementIndexOption>? parameters,
        Identifier name, SourceRef? sourceRef)
    {
        var seen = new HashSet<Type>();
        foreach (StatementIndexOption parameter in parameters ?? [])
        {
            if (!seen.Add(parameter.GetType()))
            {
                throw new SqlSyntaxException.SpecifiedMoreThanOnce(parameter.GetType().Name, "EXCLUDE index", name, sourceRef);
            }
            index = parameter switch
            {
                StatementIndexOption.FillFactor fillFactor => index with { FillFactor = fillFactor.Value },
                StatementIndexOption.PgBuffering buffering => index with { Buffering = buffering.Mode },
                StatementIndexOption.PgDeduplicateItems deduplicateItems => index with { DeduplicateItems = deduplicateItems.IsOn ?? true },
                _ => throw new SqlSyntaxException.UnsupportedDialectClause("PostgreSQL", $"EXCLUDE index option {parameter.ToSql()}", sourceRef)
            };
        }
        return index;
    }
}
