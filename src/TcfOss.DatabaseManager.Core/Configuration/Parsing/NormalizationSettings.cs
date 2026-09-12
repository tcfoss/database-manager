using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;

namespace TcfOss.DatabaseManager.Core.Configuration.Parsing;

// ReSharper disable PropertyCanBeMadeInitOnly.Global

public class NormalizationSettings
{
    public IdentifierQuotationHandling? IdentifierQuotationHandling { get; set; }
    public IdentifierQuotationHandling? CteDeclarationNameQuotationHandling { get; set; }
    public ExtendedQuoteStyle? AccountQuoteStyle { get; set; }
}
