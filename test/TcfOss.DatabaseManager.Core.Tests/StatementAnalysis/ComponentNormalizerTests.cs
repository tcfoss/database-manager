using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

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

    [Theory]
    [MemberData(nameof(CaseNormalizationCases))]
    public void NormalizeSingleIdentifier_CanonicalizesCaseWithoutChangingIdentity(NameHandling nameHandling,
        QuoteStyle inputQuote, QuoteStyle outputQuote, IdentifierQuotationHandling policy, string expectedName)
    {
        var identifier = new Identifier("MiXeD", inputQuote) { Source = new SourceRef(3, 4, 9) };
        var normalizer = new ComponentNormalizer(outputQuote, new FunctionNameProvider(), nameHandling);

        Identifier normalized = normalizer.NormalizeSingleIdentifier(identifier, policy);
        Identifier repeated = normalizer.NormalizeSingleIdentifier(normalized, policy);

        Assert.Equal(expectedName, normalized.Name);
        Assert.Equal(expectedName, Handle.GetNamePart(normalized, nameHandling));
        Assert.Equal(identifier.Source, normalized.Source);
        Assert.Equal(normalized, repeated);
        Assert.Same(normalized.OriginalIdentifiers, repeated.OriginalIdentifiers);
    }

    public static TheoryData<NameHandling, QuoteStyle, QuoteStyle, IdentifierQuotationHandling, string> CaseNormalizationCases
    {
        get
        {
            var cases = new TheoryData<NameHandling, QuoteStyle, QuoteStyle, IdentifierQuotationHandling, string>();
            foreach (NameHandling handling in Enum.GetValues<NameHandling>())
            {
                foreach (QuoteStyle input in Enum.GetValues<QuoteStyle>())
                {
                    string expected = handling switch
                    {
                        NameHandling.Lowercase => "mixed",
                        NameHandling.Uppercase => "MIXED",
                        NameHandling.LowercaseUnlessQuoted when input == QuoteStyle.None => "mixed",
                        NameHandling.UppercaseUnlessQuoted when input == QuoteStyle.None => "MIXED",
                        _ => "MiXeD"
                    };
                    foreach (QuoteStyle output in Enum.GetValues<QuoteStyle>())
                    {
                        foreach (IdentifierQuotationHandling policy in Enum.GetValues<IdentifierQuotationHandling>())
                        {
                            cases.Add(handling, input, output, policy, expected);
                        }
                    }
                }
            }
            return cases;
        }
    }

    [Theory]
    [InlineData(NameHandling.Lowercase)]
    [InlineData(NameHandling.LowercaseUnlessQuoted)]
    [InlineData(NameHandling.Uppercase)]
    [InlineData(NameHandling.UppercaseUnlessQuoted)]
    public void CaseNormalization_LeavesSigilIdentifiersUntouched(NameHandling handling)
    {
        var identifier = new Identifier("MiXeD", QuoteStyle.None, SigilKind.Variable);
        var normalizer = new ComponentNormalizer(QuoteStyle.Ansi, new FunctionNameProvider(), handling);

        Assert.Same(identifier, normalizer.NormalizeSingleIdentifier(identifier));
        Assert.Same(identifier, normalizer.NormalizeSingleIdentifier(identifier, IdentifierQuotationHandling.OnlyIfSpecial));
    }

    [Fact]
    public void NormalizeObjectIdentifier_UsesOriginalQuotingForEveryScope()
    {
        var raw = new ObjectName([new Identifier("Catalog"), new Identifier("Schema"), new Identifier("Object", QuoteStyle.Ansi)]);
        var activeSchema = new SchemaIdentifier("public", new CatalogIdentifier("catalog"));
        ObjectIdentifier identifier = ObjectIdentifier.FromObjectName(raw, activeSchema, QuoteStyle.Ansi) with { Source = new SourceRef(1, 2, 3) };
        var normalizer = new ComponentNormalizer(QuoteStyle.Ansi, new FunctionNameProvider(), NameHandling.LowercaseUnlessQuoted);

        ObjectIdentifier normalized = normalizer.NormalizeObjectIdentifier(identifier);

        Assert.Equal(("catalog", "schema", "Object"), normalized.Strings);
        Assert.Equal(identifier.Source, normalized.Source);
        Assert.Same(identifier.OriginalIdentifiers, normalized.OriginalIdentifiers);
        ObjectIdentifier repeated = normalizer.NormalizeObjectIdentifier(normalized);
        Assert.Equal(normalized, repeated);
        Assert.Same(normalized.Schema.OriginalIdentifiers, repeated.Schema.OriginalIdentifiers);
        Assert.Same(normalized.Schema.Catalog.OriginalIdentifiers, repeated.Schema.Catalog.OriginalIdentifiers);
    }

    [Fact]
    public void NormalizeColumnIdentifier_PreservesQuotedScopesAndCanonicalizesUnquotedScopes()
    {
        var raw = new ObjectName([new Identifier("Catalog", QuoteStyle.Ansi), new Identifier("Schema"), new Identifier("Table"), new Identifier("Column", QuoteStyle.Ansi)]);
        ObjectIdentifier table = ObjectIdentifier.FromStrings("catalog", "public", "table");
        ColumnIdentifier identifier = ColumnIdentifier.FromObjectName(raw, table, QuoteStyle.Ansi);
        var normalizer = new ComponentNormalizer(QuoteStyle.Ansi, new FunctionNameProvider(), NameHandling.LowercaseUnlessQuoted);

        ColumnIdentifier normalized = normalizer.NormalizeColumnIdentifier(identifier);

        Assert.Equal("Column", normalized.Name);
        Assert.Equal(("Catalog", "schema", "table"), normalized.Table.Strings);
        ColumnIdentifier repeated = normalizer.NormalizeColumnIdentifier(normalized);
        Assert.Equal(normalized, repeated);
        Assert.Same(normalized.Table.OriginalIdentifiers, repeated.Table.OriginalIdentifiers);
        Assert.Same(normalized.Table.Schema.OriginalIdentifiers, repeated.Table.Schema.OriginalIdentifiers);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void NormalizeColumnIdentifier_PropagatesAvailableOriginalScopes(int? originalCount)
    {
        SqlValueList<Identifier> originals =
        [
            new("Catalog", QuoteStyle.Ansi),
            new("Schema"),
            new("Table"),
            new("Column", QuoteStyle.Ansi)
        ];
        ColumnIdentifier identifier = ColumnIdentifier.FromStrings("Catalog", "Schema", "Table", "Column", QuoteStyle.Ansi) with
        {
            OriginalIdentifiers = originalCount.HasValue ? [.. originals[^originalCount.Value..]] : null,
            Source = new SourceRef(1, 2, 3)
        };
        var normalizer = new ComponentNormalizer(QuoteStyle.Ansi, new FunctionNameProvider(), NameHandling.LowercaseUnlessQuoted);

        ColumnIdentifier normalized = normalizer.NormalizeColumnIdentifier(identifier);

        Assert.Equal("Column", normalized.Name);
        Assert.Equal(identifier.Source, normalized.Source);
        Assert.Equal(("Catalog", originalCount >= 3 ? "schema" : "Schema", originalCount >= 2 ? "table" : "Table"), normalized.Table.Strings);
        if (originalCount >= 2)
        {
            Assert.Equal(identifier.OriginalIdentifiers![..^1], normalized.Table.OriginalIdentifiers);
        }
        else
        {
            Assert.Same(identifier.Table, Assert.Single(normalized.Table.OriginalIdentifiers!));
        }
        ColumnIdentifier repeated = normalizer.NormalizeColumnIdentifier(normalized);
        Assert.Equal(normalized, repeated);
        Assert.Same(normalized.OriginalIdentifiers, repeated.OriginalIdentifiers);
        Assert.Same(normalized.Table.OriginalIdentifiers, repeated.Table.OriginalIdentifiers);
    }

    [Theory]
    [InlineData(NameHandling.LowercaseUnlessQuoted, IdentifierQuotationHandling.Always)]
    [InlineData(NameHandling.LowercaseUnlessQuoted, IdentifierQuotationHandling.OnlyIfSpecial)]
    [InlineData(NameHandling.LowercaseUnlessQuoted, IdentifierQuotationHandling.OnlyIfSpecialOrFunction)]
    [InlineData(NameHandling.UppercaseUnlessQuoted, IdentifierQuotationHandling.Always)]
    [InlineData(NameHandling.UppercaseUnlessQuoted, IdentifierQuotationHandling.OnlyIfSpecial)]
    [InlineData(NameHandling.UppercaseUnlessQuoted, IdentifierQuotationHandling.OnlyIfSpecialOrFunction)]
    public void CaseNormalization_RestoresQuotingWhenOriginalIdentityRequiresIt(NameHandling nameHandling,
        IdentifierQuotationHandling policy)
    {
        var functions = new FunctionNameProvider();
        var original = new Identifier("MiXeD", QuoteStyle.Ansi) { Source = new SourceRef(3, 4, 9) };
        var strippingNormalizer = new ComponentNormalizer(QuoteStyle.None, functions);
        Identifier stripped = strippingNormalizer.NormalizeSingleIdentifier(original, IdentifierQuotationHandling.OnlyIfSpecial);
        Assert.Equal(QuoteStyle.None, stripped.QuoteStyle);
        Assert.Same(original, Assert.Single(stripped.OriginalIdentifiers!));
        var normalizer = new ComponentNormalizer(QuoteStyle.None, functions, nameHandling);

        Identifier normalized = normalizer.NormalizeSingleIdentifier(stripped, policy);
        Identifier normalizedWithDefaults = normalizer.NormalizeSingleIdentifier(stripped);
        Identifier repeated = normalizer.NormalizeSingleIdentifier(normalized, policy);

        Assert.Equal("MiXeD", normalized.Name);
        Assert.Equal(QuoteStyle.Ansi, normalized.QuoteStyle);
        Assert.Equal("MiXeD", Handle.GetNamePart(normalized, nameHandling));
        Assert.Equal(original.Source, normalized.Source);
        Assert.Same(stripped.OriginalIdentifiers, normalized.OriginalIdentifiers);
        Assert.Equal(normalized, normalizedWithDefaults);
        Assert.Equal(normalized, repeated);
        Assert.Same(normalized.OriginalIdentifiers, repeated.OriginalIdentifiers);
    }

    [Theory]
    [InlineData(NameHandling.LowercaseUnlessQuoted, "after")]
    [InlineData(NameHandling.UppercaseUnlessQuoted, "AFTER")]
    public void CaseNormalization_UsesCurrentNameAndOriginalQuoting(NameHandling nameHandling, string expected)
    {
        var original = new Identifier("Before", QuoteStyle.None);
        var identifier = new Identifier("AfTeR", QuoteStyle.Ansi)
        {
            OriginalIdentifiers = [original],
            Source = new SourceRef(3, 4, 9)
        };
        var normalizer = new ComponentNormalizer(QuoteStyle.Ansi, new FunctionNameProvider(), nameHandling);

        Identifier normalized = normalizer.NormalizeSingleIdentifier(identifier);

        Assert.Equal(expected, normalized.Name);
        Assert.Equal(identifier.Source, normalized.Source);
        Assert.Same(identifier.OriginalIdentifiers, normalized.OriginalIdentifiers);
        Assert.Equal(normalized, normalizer.NormalizeSingleIdentifier(normalized));
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
