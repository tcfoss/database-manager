using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public abstract record Rollback() : Statement
{
    public record Full : Rollback
    {
        public TransactionNoun? Noun { get; init; }
        public TransactionChainOption? ChainOption { get; init; }
        public TransactionReleaseOption? ReleaseOption { get; init; }
        public Identifier? Name { get; init; }

        public bool WriteAsAbort { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            if (WriteAsAbort)
            {
                writer.Write("ABORT");
            }
            else
            {
                writer.Write("ROLLBACK");
            }
            if (Noun != null)
            {
                writer.WriteSql($" {Noun}");
            }
            if (ChainOption != null)
            {
                writer.WriteSql($" {ChainOption}");
            }
            if (ReleaseOption != null)
            {
                writer.WriteSql($" {ReleaseOption}");
            }
            if (Name != null)
            {
                writer.WriteSql($" {Name}");
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Meta.FormatPreNonSql(writer, manager);
            writer.WriteSqlI(WriteAsAbort ? "ABORT" : "ROLLBACK");
            if (Noun != null)
            {
                writer.WriteSql($" {Noun}");
            }
            if (ChainOption != null)
            {
                writer.WriteSql($" {ChainOption}");
            }
            if (ReleaseOption != null)
            {
                writer.WriteSql($" {ReleaseOption}");
            }
            if (Name != null)
            {
                writer.Write(" ");
                Name.FormatSql(writer, manager);
            }
            Meta.FormatPostNonSql(writer, manager);
        }
    }

    public record Prepared(Value.StringBasedValue TransactionId) : Rollback
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"ROLLBACK PREPARED {TransactionId}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Meta.FormatPreNonSql(writer, manager);
            writer.WriteSqlI($"ROLLBACK PREPARED {TransactionId}");
            Meta.FormatPostNonSql(writer, manager);
        }
    }

    public record ToSavepoint(Identifier SavepointName) : Rollback
    {
        public bool UseSavepoint { get; init; }
        public TransactionNoun? Noun { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            // string noun = Noun == null ? "" : $" {Noun}";
            writer.Write("ROLLBACK");
            if (Noun != null)
            {
                writer.WriteSql($" {Noun}");
            }
            writer.Write(" TO");
            if (UseSavepoint)
            {
                writer.Write(" SAVEPOINT");
            }
            writer.WriteSql($" {SavepointName}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Meta.FormatPreNonSql(writer, manager);
            writer.WriteSqlI("ROLLBACK ");
            if (Noun != null)
            {
                writer.WriteSql($"{Noun} ");
            }
            writer.Write("TO ");
            if (UseSavepoint)
            {
                writer.Write("SAVEPOINT ");
            }
            SavepointName.FormatSql(writer, manager);
            Meta.FormatPostNonSql(writer, manager);
        }
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        yield break;
    }
}
