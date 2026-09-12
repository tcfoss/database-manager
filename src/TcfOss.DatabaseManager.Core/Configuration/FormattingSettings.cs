using TcfOss.DatabaseManager.Core.Configuration.Attributes;

namespace TcfOss.DatabaseManager.Core.Configuration;

public class FormattingSettings
{
    public bool PreferTabs { get; set; }
    public int TabSize { get; set; } = 4;

    public IdentifierQuotationHandling Quoting { get; set; } = IdentifierQuotationHandling.Always;
    public bool ObjectNamePrefixWithSchema { get; set; }
    public bool OmitModifiersIfDefault { get; set; } = true;
    public bool OpeningParensOnNewLine { get; set; } = true;

    public bool ExpandWildcards { get; set; }
    public bool SelectItemPrefixWithObject { get; set; } = true;
    public bool UpdateTargetPrefixWithObject { get; set; } = true;
    public bool UpdateSourcePrefixWithObject { get; set; } = true;

    public bool InsertUpdateTargetPrefixWithObject { get; set; }
    public bool InsertUpdateSourcePrefixWithObject { get; set; } = true;

    public int? JoinConditionIndent { get; set; }

    public int SpacesBeforeLineComment { get; set; } = 2;

    public int? RoutineParameterMultiLineThreshold { get; set; } = 3;
    public int? ValueListMultiLineThreshold { get; set; } = 3;
}
