using TcfOss.DatabaseManager.Core.Configuration.Attributes;

namespace TcfOss.DatabaseManager.Core.Configuration;

public record FormattingSettings
{
    public bool PreferTabs { get; init; }
    public int TabSize { get; init; } = 4;

    public IdentifierQuotationHandling Quoting { get; init; } = IdentifierQuotationHandling.Always;
    public bool ObjectNamePrefixWithSchema { get; init; }
    public bool OmitModifiersIfDefault { get; init; } = true;
    public bool OpeningParensOnNewLine { get; init; } = true;

    public bool ExpandWildcards { get; init; }
    public bool SelectItemPrefixWithObject { get; init; } = true;
    public bool UpdateTargetPrefixWithObject { get; init; } = true;
    public bool UpdateSourcePrefixWithObject { get; init; } = true;

    public bool InsertUpdateTargetPrefixWithObject { get; init; }
    public bool InsertUpdateSourcePrefixWithObject { get; init; } = true;

    public int? JoinConditionIndent { get; init; }

    public int SpacesBeforeLineComment { get; init; } = 2;

    public int? RoutineParameterMultiLineThreshold { get; init; } = 3;
    public int? ValueListMultiLineThreshold { get; init; } = 3;
}
