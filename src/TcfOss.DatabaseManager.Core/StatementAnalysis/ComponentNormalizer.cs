using System.Text.RegularExpressions;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.Expressions;

namespace TcfOss.DatabaseManager.Core.StatementAnalysis;

public partial class ComponentNormalizer(QuoteStyle quoteStyle, IFunctionNameProvider functionNameProvider) : INormalizeComponents
{
    private const string StandardIdentifierPattern = "^[A-Za-z_][A-Za-z0-9_]*$";
    private static readonly Regex s_standardIdentifierRegex = GetStandardIdentifierRegex();
    private readonly QuoteStyle _quoteStyle = quoteStyle;
    private readonly IFunctionNameProvider _functionNameProvider = functionNameProvider;

    public Identifier NormalizeSingleIdentifier(Identifier identifier)
    {
        if (identifier.Sigil != SigilKind.None)
        {
            // Sigil-bearing identifiers (e.g. variables) are never quoted.
            return identifier;
        }
        return identifier with
        {
            QuoteStyle = _quoteStyle,
            OriginalIdentifiers = identifier.OriginalIdentifiers ?? [identifier]
        };
    }

    public Identifier NormalizeSingleIdentifier(Identifier identifier, IdentifierQuotationHandling quotationHandling)
    {
        if (identifier.Sigil != SigilKind.None)
        {
            // Sigil-bearing identifiers (e.g. variables) are never quoted.
            return identifier;
        }
        if (quotationHandling == IdentifierQuotationHandling.Always)
        {
            return identifier with
            {
                QuoteStyle = _quoteStyle,
                OriginalIdentifiers = identifier.OriginalIdentifiers ?? [identifier]
            };
        }

        if (quotationHandling == IdentifierQuotationHandling.IfSpecial)
        {
            if (!s_standardIdentifierRegex.IsMatch(identifier.Name) || _functionNameProvider.IsReservedKeyword(identifier.Name))
            {
                return identifier with
                {
                    QuoteStyle = _quoteStyle,
                    OriginalIdentifiers = identifier.OriginalIdentifiers ?? [identifier]
                };
            }
            return identifier;
        }

        if (quotationHandling == IdentifierQuotationHandling.OnlyIfSpecial)
        {
            if (!s_standardIdentifierRegex.IsMatch(identifier.Name) || _functionNameProvider.IsReservedKeyword(identifier.Name))
            {
                return identifier with
                {
                    QuoteStyle = _quoteStyle,
                    OriginalIdentifiers = identifier.OriginalIdentifiers ?? [identifier]
                };
            }
            return identifier with
            {
                QuoteStyle = QuoteStyle.None,
                OriginalIdentifiers = identifier.OriginalIdentifiers ?? [identifier]
            };
        }

        if (quotationHandling == IdentifierQuotationHandling.IfSpecialOrFunction)
        {
            if (!s_standardIdentifierRegex.IsMatch(identifier.Name) || _functionNameProvider.IsReservedKeyword(identifier.Name) || _functionNameProvider.IsBuiltInFunction(identifier.Name))
            {
                return identifier with
                {
                    QuoteStyle = _quoteStyle,
                    OriginalIdentifiers = identifier.OriginalIdentifiers ?? [identifier]
                };
            }
            return identifier;
        }

        if (quotationHandling == IdentifierQuotationHandling.OnlyIfSpecialOrFunction)
        {
            if (!s_standardIdentifierRegex.IsMatch(identifier.Name) || _functionNameProvider.IsReservedKeyword(identifier.Name) || _functionNameProvider.IsBuiltInFunction(identifier.Name))
            {
                return identifier with
                {
                    QuoteStyle = _quoteStyle,
                    OriginalIdentifiers = identifier.OriginalIdentifiers ?? [identifier]
                };
            }
            return identifier with
            {
                QuoteStyle = QuoteStyle.None,
                OriginalIdentifiers = identifier.OriginalIdentifiers ?? [identifier]
            };
        }

        // LeaveOriginal
        return identifier;
    }

    public Identifier? NormalizeSingleIdentifierNullable(Identifier? identifier)
    {
        if (identifier is null)
        {
            return null;
        }

        return NormalizeSingleIdentifier(identifier);
    }

    public ObjectName NormalizeObjectName(ObjectName name)
    {
        return new ObjectName([.. name.Values.Select(NormalizeSingleIdentifier)]);
    }

    public CompoundIdentifier NormalizeIdentifier(CompoundIdentifier identifier)
    {
        return new CompoundIdentifier([.. identifier.Identifiers.Select(NormalizeSingleIdentifier)]);
    }

    public SingleIdentifier NormalizeIdentifier(SingleIdentifier identifier)
    {
        return new SingleIdentifier(NormalizeSingleIdentifier(identifier.Identifier));
    }

    public virtual Value NormalizeValue(Value value)
    {
        return value;
    }

    [GeneratedRegex(StandardIdentifierPattern, RegexOptions.Compiled)]
    private static partial Regex GetStandardIdentifierRegex();
}
