using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

/// <summary>
/// T-SQL table hint variants used inside a <c>WITH (...)</c> clause on a
/// table reference. Hints are dialect-specific; the abstract base
/// <see cref="TableHint"/> lives alongside this type so
/// <see cref="TableFactor.Table"/> can hold them and the core parser can
/// construct them without taking a dependency on the T-SQL project.
/// </summary>
public abstract record MsTableHint : TableHint
{
    /// <summary>
    /// A simple keyword-style hint such as <c>NOLOCK</c>, <c>READPAST</c>,
    /// <c>HOLDLOCK</c>, <c>TABLOCK</c>, etc. Stored as an identifier so any
    /// hint name can round-trip without enumerating every supported value.
    /// </summary>
    public record Simple(Identifier Name) : MsTableHint
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Name}");
        }
    }

    /// <summary>
    /// An <c>INDEX(idx1, idx2, ...)</c> hint specifying which indexes the
    /// query optimizer should consider for the table reference.
    /// </summary>
    public record Index(SqlValueList<Identifier> Indexes) : MsTableHint
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"INDEX({Indexes})");
        }
    }
}
