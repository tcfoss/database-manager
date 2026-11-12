using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record StatementIndexOption : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);

    public virtual void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        ToSql(writer);
    }

    public record OnOffOption(string OptionName, bool IsOn) : StatementIndexOption
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{OptionName} = {(IsOn ? "ON" : "OFF")}");
        }
    }

    public record PadIndex(bool IsOn) : OnOffOption("PAD_INDEX", IsOn);

    public record FillFactor(uint Value) : StatementIndexOption
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"FILLFACTOR = {Value}");
        }
    }

    public record IgnoreDupKey(bool IsOn) : OnOffOption("IGNORE_DUP_KEY", IsOn);

    public record StatisticsNoRecompute(bool IsOn) : OnOffOption("STATISTICS_NORECOMPUTE", IsOn);

    public record StatisticsIncremental(bool IsOn) : OnOffOption("STATISTICS_INCREMENTAL", IsOn);

    public record AllowRowLocks(bool IsOn) : OnOffOption("ALLOW_ROW_LOCKS", IsOn);

    public record AllowPageLocks(bool IsOn) : OnOffOption("ALLOW_PAGE_LOCKS", IsOn);

    public record OptimizeForSequentialKey(bool IsOn) : OnOffOption("OPTIMIZE_FOR_SEQUENTIAL_KEY", IsOn);

    public abstract record PgBooleanOption(string OptionName, bool? IsOn) : StatementIndexOption
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write(OptionName);
            if (IsOn != null)
            {
                writer.Write(IsOn.Value ? " = ON" : " = OFF");
            }
        }
    }

    public record PgDeduplicateItems(bool? IsOn) : PgBooleanOption("DEDUPLICATE_ITEMS", IsOn);

    public record PgFastUpdate(bool? IsOn) : PgBooleanOption("FASTUPDATE", IsOn);

    public record PgAutosummarize(bool? IsOn) : PgBooleanOption("AUTOSUMMARIZE", IsOn);

    public record PgBuffering(PgIndexBufferingMode Mode) : StatementIndexOption
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"BUFFERING = {Mode}");
        }
    }

    public record PgGinPendingListLimit(uint Value) : StatementIndexOption
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"GIN_PENDING_LIST_LIMIT = {Value}");
        }
    }

    public record PgPagesPerRange(uint Value) : StatementIndexOption
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"PAGES_PER_RANGE = {Value}");
        }
    }

    public record StorageParameter(PgStorageParameter Parameter) : StatementIndexOption
    {
        public override void ToSql(SqlTextWriter writer)
        {
            Parameter.ToSql(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Parameter.FormatSql(writer, manager);
        }
    }
}
