using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;

namespace TcfOss.DatabaseManager.Core.Configuration.Parsing;

public class Config
{
    public string? ProjectDirectory { get; set; }

    public string? Catalog { get; set; }
    public required SqlDialect Dialect { get; set; }
    public string? Version { get; set; }
    public QuoteStyle? QuoteStyle { get; set; }

    public SchemaMapping[] Schemas { get; set; } = [];

    public FormattingSettings? Formatting { get; set; }
    public DifferFormattingSettings? DifferFormatting { get; set; }

    public Credentials? Credentials { get; set; }

    public string? DefaultDefinerAccount { get; set; }
    public string? DefaultDefinerHost { get; set; }

    public LogSettings Logging { get; set; } = new();

    // The following three settings are especially advanced and are not
    // currently documented.
    public ParseSettings? ParseSettings { get; set; }
    public NormalizationSettings? ViewNormalizationSettings { get; set; }
    public AttributeDefaults? AttributeDefaults { get; set; }

    // Database connection pooling settings
    public uint MinPoolSize { get; set; }
    public uint MaxPoolSize { get; set; } = 100;
}
