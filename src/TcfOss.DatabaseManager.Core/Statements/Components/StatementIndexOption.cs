using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record StatementIndexOption : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);

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
}
