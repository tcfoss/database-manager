using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.MsSql.Statements.Components;

/// <summary>
/// One argument of a T-SQL <c>EXEC[UTE]</c> procedure invocation. Modeled as
/// a sealed hierarchy of two mutually-exclusive variants:
/// <list type="bullet">
///   <item><description>
///     <see cref="ExpressionValue"/>: an arbitrary expression value.
///   </description></item>
///   <item><description>
///     <see cref="DefaultValue"/>: the literal keyword <c>DEFAULT</c>.
///   </description></item>
/// </list>
/// Either variant may be prefixed with <c>@name =</c> (named binding) and/or
/// suffixed with <c>OUTPUT</c>.
/// </summary>
public abstract record MsExecuteArgument : IWriteSql
{
    public Identifier? Name { get; init; }
    public bool IsOutput { get; init; }

    public abstract void ToSql(SqlTextWriter writer);

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        ToSql(writer);
    }

    public virtual IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        yield break;
    }

    public record ExpressionValue(Expression Value) : MsExecuteArgument
    {
        public override void ToSql(SqlTextWriter writer)
        {
            if (Name != null)
            {
                writer.WriteSql($"{Name} = ");
            }
            writer.WriteSql($"{Value}");
            if (IsOutput)
            {
                writer.Write(" OUTPUT");
            }
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            foreach (ItemRef r in Value.GetReferencedItems(context))
            {
                yield return r;
            }
        }
    }

    public record DefaultValue : MsExecuteArgument
    {
        public override void ToSql(SqlTextWriter writer)
        {
            if (Name != null)
            {
                writer.WriteSql($"{Name} = ");
            }
            writer.Write("DEFAULT");
            if (IsOutput)
            {
                writer.Write(" OUTPUT");
            }
        }
    }
}
