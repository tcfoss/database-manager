using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;

namespace TcfOss.DatabaseManager.Core.Configuration;

public class NormalizationSettings
{
    public IdentifierQuotationHandling IdentifierQuotationHandling { get; init; } = IdentifierQuotationHandling.Always;
    public IdentifierQuotationHandling CteDeclarationNameQuotationHandling { get; init; } = IdentifierQuotationHandling.LeaveOriginal;
    public ExtendedQuoteStyle AccountQuoteStyle { get; init; }
    public bool CastConvertVarcharToChar { get; init; }
    public bool CastAddCharsetToType { get; init; }
}
