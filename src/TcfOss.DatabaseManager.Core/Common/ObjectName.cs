using Newtonsoft.Json;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Common;

[method: JsonConstructor]
public sealed record ObjectName(SqlValueList<Identifier> Values) : IWriteSql
{
    public SourceRef? Source => SourceRef.FromIdentifiers(Values);

    public ObjectName(Identifier name) : this([name]) { }

    public override string ToString()
    {
        return string.Join<Identifier>(".", Values);
    }

    public void ToSql(SqlTextWriter writer)
    {
        for (int i = 0; i < Values.Count; i++)
        {
            if (i > 0)
            {
                writer.Write('.');
            }
            Values[i].ToSql(writer);
        }
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        for (int i = 0; i < Values.Count; i++)
        {
            if (i > 0)
            {
                writer.Write('.');
            }
            writer.Write(manager.GetQuotedIdentifier(Values[i]));
        }
    }
}
