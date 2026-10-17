using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Tests.StatementAnalysis;

public class ComponentNormalizerTests
{
    [Fact]
    public void NormalizeSingleIdentifier_WithoutSigil_WithOriginalIdentifiers_Quoted_CarriedOriginals()
    {
        var identifier = GetIdentifier("id", QuoteStyle.None, SigilKind.None);
        var normalizer = new ComponentNormalizer(QuoteStyle.Backticks, new FunctionNameProvider());
        var normalizedIdentifier = normalizer.NormalizeSingleIdentifier(identifier);
        Assert.Equal(QuoteStyle.Backticks, normalizedIdentifier.QuoteStyle);
        Assert.Equal(s_originalIdentifiers, normalizedIdentifier.OriginalIdentifiers);
    }

    [Fact]
    public void NormalizeSingleIdentifier_WithoutSigil_WithoutOriginalIdentifiers_Quoted_SelfAsOriginals()
    {
        var identifier = GetIdentifier("id", QuoteStyle.None, SigilKind.None, includeOriginalIdentifiers: false);
        var normalizer = new ComponentNormalizer(QuoteStyle.Backticks, new FunctionNameProvider());
        var normalizedIdentifier = normalizer.NormalizeSingleIdentifier(identifier);
        Assert.Equal(QuoteStyle.Backticks, normalizedIdentifier.QuoteStyle);
        Assert.Equal([identifier], normalizedIdentifier.OriginalIdentifiers);
    }

    [Fact]
    public void NormalizeSingleIdentifier_WithSigil_WithOriginalIdentifiers_NotQuoted_CarriedOriginals()
    {
        var identifier = GetIdentifier("id", QuoteStyle.None, SigilKind.Variable);
        var normalizer = new ComponentNormalizer(QuoteStyle.Backticks, new FunctionNameProvider());
        var normalizedIdentifier = normalizer.NormalizeSingleIdentifier(identifier);
        Assert.Equal(QuoteStyle.None, normalizedIdentifier.QuoteStyle);
        Assert.Equal(s_originalIdentifiers, normalizedIdentifier.OriginalIdentifiers);
    }

    [Fact]
    public void NormalizeSingleIdentifier_WithSigil_WithoutOriginalIdentifiers_NotQuoted_NullOriginals()
    {
        var identifier = GetIdentifier("id", QuoteStyle.None, SigilKind.Variable, includeOriginalIdentifiers: false);
        var normalizer = new ComponentNormalizer(QuoteStyle.Backticks, new FunctionNameProvider());
        var normalizedIdentifier = normalizer.NormalizeSingleIdentifier(identifier);
        Assert.Equal(QuoteStyle.None, normalizedIdentifier.QuoteStyle);
        Assert.Null(normalizedIdentifier.OriginalIdentifiers);
    }

    [Theory]
    [MemberData(nameof(QuotationCases))]
    public void NormalizeSingleIdentifier_QuoteNameHandling(string name, QuoteStyle quoteStyle, QuoteStyle defaultQuoteStyle,
        IdentifierQuotationHandling quoteHandling, bool includeOriginalIdentifiers, QuoteStyle expectedQuoteStyle, bool preserveOriginal)
    {
        var identifier = GetIdentifier(name, quoteStyle, SigilKind.None, includeOriginalIdentifiers);
        var normalizer = new ComponentNormalizer(defaultQuoteStyle, new FunctionNameProvider());

        var normalizedIdentifier = normalizer.NormalizeSingleIdentifier(identifier, quoteHandling);

        Assert.Equal(name, normalizedIdentifier.Name);
        Assert.Equal(SigilKind.None, normalizedIdentifier.Sigil);
        Assert.Equal(expectedQuoteStyle, normalizedIdentifier.QuoteStyle);
        if (preserveOriginal)
        {
            Assert.Same(identifier, normalizedIdentifier);
        }
        else if (includeOriginalIdentifiers)
        {
            Assert.Same(s_originalIdentifiers, normalizedIdentifier.OriginalIdentifiers);
        }
        else
        {
            Assert.NotNull(normalizedIdentifier.OriginalIdentifiers);
            Assert.Same(identifier, Assert.Single(normalizedIdentifier.OriginalIdentifiers));
        }

        var normalizedAgain = normalizer.NormalizeSingleIdentifier(normalizedIdentifier, quoteHandling);
        Assert.Equal(normalizedIdentifier, normalizedAgain);
        Assert.Same(normalizedIdentifier.OriginalIdentifiers, normalizedAgain.OriginalIdentifiers);
    }

    public static TheoryData<string, QuoteStyle, QuoteStyle, IdentifierQuotationHandling, bool, QuoteStyle, bool> QuotationCases
    {
        get
        {
            (IdentifierQuotationHandling Handling, QuotationAction Ordinary, QuotationAction Special, QuotationAction Function)[] rules =
            [
                (IdentifierQuotationHandling.Always, QuotationAction.Apply, QuotationAction.Apply, QuotationAction.Apply),
                (IdentifierQuotationHandling.IfSpecial, QuotationAction.Keep, QuotationAction.Apply, QuotationAction.Keep),
                (IdentifierQuotationHandling.OnlyIfSpecial, QuotationAction.Strip, QuotationAction.Apply, QuotationAction.Strip),
                (IdentifierQuotationHandling.IfSpecialOrFunction, QuotationAction.Keep, QuotationAction.Apply, QuotationAction.Apply),
                (IdentifierQuotationHandling.OnlyIfSpecialOrFunction, QuotationAction.Strip, QuotationAction.Apply, QuotationAction.Apply),
                (IdentifierQuotationHandling.LeaveOriginal, QuotationAction.Keep, QuotationAction.Keep, QuotationAction.Keep)
            ];
            string[] ordinaryNames = ["colname", "_", "_column1", "Column_123", "select_column", "current_user_id"];
            string[] specialNames = ["select", "SELECT", "SeLeCt", "my name", "1column", "column-name", "column.name", "column\n", ""];
            string[] functionNames = ["current_user", "CURRENT_USER", "CuRrEnT_UsEr", "count"];
            var cases = new TheoryData<string, QuoteStyle, QuoteStyle, IdentifierQuotationHandling, bool, QuoteStyle, bool>();

            foreach (var (handling, ordinary, special, function) in rules)
            {
                (string[] Names, QuotationAction Action)[] groups =
                [
                    (ordinaryNames, ordinary),
                    (specialNames, special),
                    (functionNames, function)
                ];
                foreach (var group in groups)
                {
                    foreach (var name in group.Names)
                    {
                        foreach (var quoteStyle in Enum.GetValues<QuoteStyle>())
                        {
                            foreach (var defaultQuoteStyle in Enum.GetValues<QuoteStyle>())
                            {
                                var expectedQuoteStyle = group.Action switch
                                {
                                    QuotationAction.Apply => defaultQuoteStyle,
                                    QuotationAction.Strip => QuoteStyle.None,
                                    _ => quoteStyle
                                };
                                foreach (var includeOriginalIdentifiers in new[] { true, false })
                                {
                                    cases.Add(name, quoteStyle, defaultQuoteStyle, handling, includeOriginalIdentifiers,
                                        expectedQuoteStyle, group.Action == QuotationAction.Keep);
                                }
                            }
                        }
                    }
                }
            }

            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(DefaultQuotationCases))]
    public void NormalizeSingleIdentifier_WithoutHandling_AppliesDefaultQuote(
        QuoteStyle quoteStyle, QuoteStyle defaultQuoteStyle, bool includeOriginalIdentifiers)
    {
        var identifier = GetIdentifier(quoteStyle: quoteStyle, includeOriginalIdentifiers: includeOriginalIdentifiers);
        var normalizer = new ComponentNormalizer(defaultQuoteStyle, new FunctionNameProvider());

        var normalizedIdentifier = normalizer.NormalizeSingleIdentifier(identifier);

        Assert.Equal(identifier.Name, normalizedIdentifier.Name);
        Assert.Equal(identifier.Sigil, normalizedIdentifier.Sigil);
        Assert.Equal(defaultQuoteStyle, normalizedIdentifier.QuoteStyle);
        if (includeOriginalIdentifiers)
        {
            Assert.Same(s_originalIdentifiers, normalizedIdentifier.OriginalIdentifiers);
        }
        else
        {
            Assert.NotNull(normalizedIdentifier.OriginalIdentifiers);
            Assert.Same(identifier, Assert.Single(normalizedIdentifier.OriginalIdentifiers));
        }

        var normalizedAgain = normalizer.NormalizeSingleIdentifier(normalizedIdentifier);
        Assert.Equal(normalizedIdentifier, normalizedAgain);
        Assert.Same(normalizedIdentifier.OriginalIdentifiers, normalizedAgain.OriginalIdentifiers);
    }

    public static TheoryData<QuoteStyle, QuoteStyle, bool> DefaultQuotationCases
    {
        get
        {
            var cases = new TheoryData<QuoteStyle, QuoteStyle, bool>();
            foreach (var quoteStyle in Enum.GetValues<QuoteStyle>())
            {
                foreach (var defaultQuoteStyle in Enum.GetValues<QuoteStyle>())
                {
                    cases.Add(quoteStyle, defaultQuoteStyle, true);
                    cases.Add(quoteStyle, defaultQuoteStyle, false);
                }
            }

            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(SigilCases))]
    public void NormalizeSingleIdentifier_WithSigil_LeavesOriginal(
        string name, QuoteStyle defaultQuoteStyle, IdentifierQuotationHandling? quoteHandling, bool includeOriginalIdentifiers)
    {
        var identifier = GetIdentifier(name, QuoteStyle.None, SigilKind.Variable, includeOriginalIdentifiers);
        var normalizer = new ComponentNormalizer(defaultQuoteStyle, new FunctionNameProvider());

        var normalizedIdentifier = quoteHandling.HasValue
            ? normalizer.NormalizeSingleIdentifier(identifier, quoteHandling.Value)
            : normalizer.NormalizeSingleIdentifier(identifier);

        Assert.Same(identifier, normalizedIdentifier);
        Assert.Equal(QuoteStyle.None, normalizedIdentifier.QuoteStyle);
        Assert.Same(identifier.OriginalIdentifiers, normalizedIdentifier.OriginalIdentifiers);
    }

    public static TheoryData<string, QuoteStyle, IdentifierQuotationHandling?, bool> SigilCases
    {
        get
        {
            var cases = new TheoryData<string, QuoteStyle, IdentifierQuotationHandling?, bool>();
            IdentifierQuotationHandling?[] policies = [null, .. Enum.GetValues<IdentifierQuotationHandling>().Select(handling => (IdentifierQuotationHandling?)handling)];
            foreach (var name in new[] { "colname", "select", "current_user", "my name" })
            {
                foreach (var defaultQuoteStyle in Enum.GetValues<QuoteStyle>())
                {
                    foreach (var policy in policies)
                    {
                        cases.Add(name, defaultQuoteStyle, policy, true);
                        cases.Add(name, defaultQuoteStyle, policy, false);
                    }
                }
            }

            return cases;
        }
    }

    [Theory]
    [InlineData(nameof(Value.Boolean), "true", false)]
    [InlineData(nameof(Value.Boolean), "false", false)]
    [InlineData(nameof(Value.Number), "42", false)]
    [InlineData(nameof(Value.Number), "42", true)]
    [InlineData(nameof(Value.Number), "36.4", false)]
    [InlineData(nameof(Value.SingleQuotedString), "Hello", false)]
    [InlineData(nameof(Value.NationalStringLiteral), "Hello", false)]
    [InlineData(nameof(Value.HexString), "DEADBEEF", false)]
    [InlineData(nameof(Value.Null), "", false)]
    public void NormalizeValue_LeavesOriginal(string valueKind, string text, bool isLong)
    {
        Value value = valueKind switch
        {
            nameof(Value.Boolean) => new Value.Boolean(bool.Parse(text)),
            nameof(Value.Number) => new Value.Number(text, isLong),
            nameof(Value.SingleQuotedString) => new Value.SingleQuotedString(text),
            nameof(Value.NationalStringLiteral) => new Value.NationalStringLiteral(text),
            nameof(Value.HexString) => new Value.HexString(text),
            nameof(Value.Null) => new Value.Null(),
            _ => throw new ArgumentOutOfRangeException(nameof(valueKind), valueKind, null)
        };
        var normalizer = new ComponentNormalizer(QuoteStyle.Ansi, new FunctionNameProvider());
        var normalizedValue = normalizer.NormalizeValue(value);

        Assert.Same(value, normalizedValue);
    }

    private enum QuotationAction
    {
        Keep,
        Apply,
        Strip
    }

    private static readonly SqlValueList<Identifier> s_originalIdentifiers = [new("id1", QuoteStyle.None), new("id2", QuoteStyle.None)];

    private static Identifier GetIdentifier(string name = "id", QuoteStyle quoteStyle = QuoteStyle.Ansi, SigilKind sigilKind = SigilKind.None,
        bool includeOriginalIdentifiers = true)
    {
        var id = new Identifier(name, quoteStyle, sigilKind)
        {
            OriginalIdentifiers = includeOriginalIdentifiers ? s_originalIdentifiers : null
        };
        return id;
    }
}
