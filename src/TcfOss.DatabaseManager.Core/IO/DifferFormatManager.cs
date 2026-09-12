using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.DatabaseObjects;

namespace TcfOss.DatabaseManager.Core.IO;

public record DifferFormatManager
{
    public required QuoteStyle QuoteStyle { get; init; } = QuoteStyle.None;
    public required DifferFormattingSettings Formatting { get; init; }
    public required AttributeDefaults AttributeDefaults { get; init; }
    public ITable? Table { get; init; }

    public bool OmitForeignKeysOnCreateTable { get; init; }
}
