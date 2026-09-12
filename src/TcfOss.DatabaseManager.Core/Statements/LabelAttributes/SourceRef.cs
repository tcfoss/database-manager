using TcfOss.DatabaseManager.Core.Common;

namespace TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

public readonly record struct SourceRef(int SourceId, int Start, int End)
{
    public static SourceRef? FromIdentifiers(SqlValueList<Identifier> identifiers)
    {
        if (identifiers.Count == 0)
        {
            return null;
        }

        if (identifiers.Any(x => x.Source == null))
        {
            return null;
        }

        if (identifiers.Count == 1)
        {
            return identifiers[0].Source;
        }

        return new SourceRef(identifiers[0].Source!.Value.SourceId, identifiers[0].Source!.Value.Start, identifiers[^1].Source!.Value.End);
    }
}
