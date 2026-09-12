using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public abstract record DeclareLocalVariable : Statement
{
    public record My(SqlValueList<Identifier> Names, DataType DataType) : DeclareLocalVariable
    {
        public Expression? DefaultValue { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"DECLARE {Names} {DataType}");
            if (DefaultValue != null)
            {
                writer.WriteSql($" DEFAULT {DefaultValue}");
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Meta.FormatPreNonSql(writer, manager);
            writer.WriteSqlI("DECLARE ");
            writer.FormatDelimited(Names, manager);
            writer.WriteSql($" {DataType}");
            if (DefaultValue != null)
            {
                writer.Write(" DEFAULT ");
                DefaultValue.FormatSql(writer, manager);
            }
            Meta.FormatPostNonSql(writer, manager);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            foreach (Identifier name in Names)
            {
                context.AddLocalVariable(name);
            }

            if (DefaultValue != null)
            {
                foreach (ItemRef item in DefaultValue.GetReferencedItems(context))
                {
                    yield return item;
                }
            }
        }
    }

    public record Ms(SqlValueList<MsVariableDeclaration> Declarations) : DeclareLocalVariable
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"DECLARE {Declarations}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Meta.FormatPreNonSql(writer, manager);
            writer.WriteSqlI("DECLARE ");
            writer.FormatDelimited(Declarations, manager);
            Meta.FormatPostNonSql(writer, manager);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            foreach (MsVariableDeclaration declaration in Declarations)
            {
                context.AddLocalVariable(declaration.Name);
                if (declaration.InitialValue != null)
                {
                    foreach (ItemRef item in declaration.InitialValue.GetReferencedItems(context))
                    {
                        yield return item;
                    }
                }
            }
        }
    }
}

