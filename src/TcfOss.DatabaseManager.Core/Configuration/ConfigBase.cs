using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;

namespace TcfOss.DatabaseManager.Core.Configuration;

public class ConfigBase
{
    public required string ProjectDirectory { get; init; }
    public required CatalogIdentifier Catalog { get; init; }
    public SqlDialect Dialect { get; init; }
    public QuoteStyle QuoteStyle { get; init; }
    public required bool DatabaseAvailable { get; init; }
    public required ValidationSettings ValidationSettings { get; init; }
    public AttributeDefaults AttributeDefaults { get; init; } = new();
    public ParseSettings ParseSettings { get; init; } = new();
    public NormalizationSettings NormalizationSettings { get; init; } = new();
    public FormattingSettings Formatting { get; init; } = new();
    public DifferFormattingSettings DifferFormatting { get; init; } = new();
    public NameHandling NameHandling { get; init; }

    public LogSettings Logging { get; init; } = new();

    public uint MinPoolSize { get; init; }
    public uint MaxPoolSize { get; init; }
}
