using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.MsSql.Statements.Components;

namespace TcfOss.DatabaseManager.MsSql.Statements;

/// <summary>
/// T-SQL <c>EXEC[UTE]</c> statement. Modeled as a sealed hierarchy of two
/// mutually-exclusive variants:
/// <list type="bullet">
///   <item><description>
///     <see cref="ProcedureCall"/>: <c>EXEC [@ret =] proc_name [arg [, ...]]</c>.
///   </description></item>
///   <item><description>
///     <see cref="DynamicSqlCall"/>: <c>EXEC (string_expression)</c>.
///   </description></item>
/// </list>
/// </summary>
public abstract record MsExecute : Statement
{
    /// <summary>
    /// Whether the source spelled the keyword as <c>EXECUTE</c> (true) or
    /// <c>EXEC</c> (false). Preserved for round-trip fidelity.
    /// </summary>
    public bool UseExecute { get; init; }

    private string Keyword => UseExecute ? "EXECUTE" : "EXEC";

    public record ProcedureCall(ObjectName ProcedureName) : MsExecute
    {
        public Identifier? ReturnVariable { get; init; }
        public SqlValueList<MsExecuteArgument>? Arguments { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write(Keyword);
            writer.Write(' ');
            if (ReturnVariable != null)
            {
                writer.WriteSql($"{ReturnVariable} = ");
            }
            writer.WriteSql($"{ProcedureName}");
            if (Arguments.SafeAny())
            {
                writer.WriteSql($" {Arguments}");
            }
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            ItemRef? procRef = context.CreateFunctionOrProcRef(ProcedureName);
            if (procRef != null)
            {
                yield return procRef;
            }

            if (Arguments != null)
            {
                foreach (MsExecuteArgument arg in Arguments)
                {
                    foreach (ItemRef r in arg.GetReferencedItems(context))
                    {
                        yield return r;
                    }
                }
            }
        }
    }

    public record DynamicSqlCall(Expression DynamicSql) : MsExecute
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write(Keyword);
            writer.WriteSql($" ({DynamicSql})");
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            foreach (ItemRef r in DynamicSql.GetReferencedItems(context))
            {
                yield return r;
            }
        }
    }
}
