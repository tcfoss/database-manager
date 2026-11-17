using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;
using TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

namespace TcfOss.DatabaseManager.Postgres.DefinitionBuilding;

public static class PgConstraintNormalizer
{
    public static PgConstraintOptions Normalize(SqlValueList<PgConstraintOption>? options, string constraintType,
        Identifier owner, SourceRef? sourceRef, bool allowDeferrable = false, bool allowNoInherit = false)
    {
        bool? deferrable = null;
        bool initiallyDeferred = false;
        bool noInherit = false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (PgConstraintOption option in options ?? [])
        {
            if (!seen.Add(option.DuplicateCheckKey))
            {
                throw new SqlSyntaxException.SpecifiedMoreThanOnce(option.DuplicateCheckKey, constraintType, owner, sourceRef);
            }
            switch (option)
            {
                case PgConstraintOption.Deferrable value when allowDeferrable || !value.Value:
                    deferrable = value.Value;
                    break;
                case PgConstraintOption.InitiallyDeferred value when allowDeferrable || !value.Value:
                    initiallyDeferred = value.Value;
                    break;
                case PgConstraintOption.NoInherit when allowNoInherit:
                    noInherit = true;
                    break;
                default:
                    throw new SqlSyntaxException.UnsupportedDialectClause("PostgreSQL",
                        $"{option.ToSql()} on CREATE TABLE {constraintType}", sourceRef);
            }
        }
        if (deferrable == false && initiallyDeferred)
        {
            throw new SqlSyntaxException.MutuallyExclusive("NOT DEFERRABLE", "INITIALLY DEFERRED",
                constraintType, owner.ToString(), sourceRef);
        }
        return new PgConstraintOptions
        {
            Deferrable = deferrable ?? initiallyDeferred,
            InitiallyDeferred = initiallyDeferred,
            NoInherit = noInherit
        };
    }
}
