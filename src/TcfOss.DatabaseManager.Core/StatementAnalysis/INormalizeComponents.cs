using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.Expressions;

namespace TcfOss.DatabaseManager.Core.StatementAnalysis;

// Many attributes are accessed only via more specific type (for now), but we'll keep them on
// the interface for the sake of code using this as a library.
// ReSharper disable UnusedMemberInSuper.Global
public interface INormalizeComponents
{
    Identifier NormalizeSingleIdentifier(Identifier identifier);
    Identifier NormalizeSingleIdentifier(Identifier identifier, IdentifierQuotationHandling quotationHandling);
    Identifier? NormalizeSingleIdentifierNullable(Identifier? identifier);
    ObjectName NormalizeObjectName(ObjectName name);
    CompoundIdentifier NormalizeIdentifier(CompoundIdentifier identifier);
    SingleIdentifier NormalizeIdentifier(SingleIdentifier identifier);
    Value NormalizeValue(Value value);
}
