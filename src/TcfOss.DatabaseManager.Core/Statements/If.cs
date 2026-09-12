using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

#pragma warning disable CA1716 // Identifiers should not match keywords

namespace TcfOss.DatabaseManager.Core.Statements;

/// <summary>
/// Base class for <c>IF</c> statements. Use <see cref="My"/> for the
/// MySQL/standard blocked form (<c>IF … THEN … END IF</c>) and
/// <see cref="Ms"/> for the T-SQL inline form
/// (<c>IF &lt;cond&gt; &lt;stmt&gt; [ELSE &lt;stmt&gt;]</c>).
/// </summary>
public abstract record If(Expression Condition) : Statement
{

    /// <summary>
    /// MySQL/standard <c>IF</c>: <c>IF &lt;cond&gt; THEN … [ELSEIF … THEN …]* [ELSE …] END IF</c>.
    /// </summary>
    public record My(Expression Condition, SqlValueList<Statement> Statements) : If(Condition)
    {
        public SqlValueList<ElseIf>? ElseIfs { get; init; }
        public SqlValueList<Statement>? ElseStatements { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"IF {Condition} THEN ");
            writer.WriteTerminated(Statements.NonInert());
            if (ElseIfs.SafeAny())
            {
                writer.WriteDelimited(ElseIfs, "");
            }
            if (ElseStatements.SafeAny())
            {
                writer.Write("ELSE ");
                writer.WriteTerminated(ElseStatements.NonInert());
            }
            writer.Write("END IF");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Meta.FormatPreNonSql(writer, manager);
            manager.WriteBlockPart(writer, "IF");
            writer.WriteSql($" {Condition} THEN");
            manager.IncreaseIndent();
            foreach (Statement stmt in Statements)
            {
                stmt.FormatSql(writer, manager);
                manager.TerminateStatement(writer, stmt);
            }
            manager.DecreaseIndent();
            if (ElseIfs.SafeAny())
            {
                foreach (ElseIf elseIf in ElseIfs!)
                {
                    elseIf.FormatSql(writer, manager);
                }
            }
            if (ElseStatements.SafeAny())
            {
                manager.WriteBlockPart(writer, "ELSE");
                manager.IncreaseIndent();
                foreach (Statement stmt in ElseStatements!)
                {
                    stmt.FormatSql(writer, manager);
                    manager.TerminateStatement(writer, stmt);
                }
                manager.DecreaseIndent();
            }
            manager.WriteBlockPart(writer, "END IF");
            Meta.FormatPostNonSql(writer, manager);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            foreach (ItemRef item in Condition.GetReferencedItems(context))
            {
                yield return item;
            }

            foreach (Statement stmt in Statements)
            {
                foreach (ItemRef item in stmt.GetReferencedItems(context))
                {
                    yield return item;
                }
            }

            if (ElseIfs != null)
            {
                foreach (ElseIf elseIf in ElseIfs)
                {
                    foreach (ItemRef item in elseIf.GetReferencedItems(context))
                    {
                        yield return item;
                    }
                }
            }

            if (ElseStatements != null)
            {
                foreach (Statement stmt in ElseStatements)
                {
                    foreach (ItemRef item in stmt.GetReferencedItems(context))
                    {
                        yield return item;
                    }
                }
            }
        }
    }

    /// <summary>
    /// T-SQL <c>IF</c>: <c>IF &lt;cond&gt; &lt;stmt&gt; [ELSE &lt;stmt&gt;]</c>.
    /// No <c>THEN</c>, no <c>ELSEIF</c>, no <c>END IF</c>; each branch is a
    /// single un-terminated statement.
    /// </summary>
    public record Ms(Expression Condition, Statement Statement) : If(Condition)
    {
        public Statement? ElseStatement { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"IF {Condition} ");
            Statement.ToSql(writer);
            if (ElseStatement != null)
            {
                writer.Write(" ELSE ");
                ElseStatement.ToSql(writer);
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Meta.FormatPreNonSql(writer, manager);
            manager.WriteBlockPart(writer, "IF");
            writer.Write(" ");
            Condition.FormatSql(writer, manager);
            writer.Write(" ");
            Statement.FormatSql(writer, manager);
            if (ElseStatement != null)
            {
                writer.Write(" ELSE ");
                ElseStatement.FormatSql(writer, manager);
            }
            Meta.FormatPostNonSql(writer, manager);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            foreach (ItemRef item in Condition.GetReferencedItems(context))
            {
                yield return item;
            }

            foreach (ItemRef item in Statement.GetReferencedItems(context))
            {
                yield return item;
            }

            if (ElseStatement != null)
            {
                foreach (ItemRef item in ElseStatement.GetReferencedItems(context))
                {
                    yield return item;
                }
            }
        }
    }

    public record ElseIf(Expression Condition, SqlValueList<Statement> Statements) : Statement
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"ELSEIF {Condition} THEN ");
            writer.WriteTerminated(Statements.NonInert());
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            manager.WriteBlockPart(writer, "ELSEIF");
            writer.WriteSql($" {Condition} THEN");
            manager.IncreaseIndent();
            foreach (Statement stmt in Statements)
            {
                stmt.FormatSql(writer, manager);
                manager.TerminateStatement(writer, stmt);
            }
            manager.DecreaseIndent();
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            foreach (ItemRef item in Condition.GetReferencedItems(context))
            {
                yield return item;
            }

            foreach (Statement stmt in Statements)
            {
                foreach (ItemRef item in stmt.GetReferencedItems(context))
                {
                    yield return item;
                }
            }
        }
    }
}
