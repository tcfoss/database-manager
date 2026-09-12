using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.Expressions.Attributes;

[StringEnum("IN NATURAL LANGUAGE MODE", "NaturalLanguageMode")]
[StringEnum("IN BOOLEAN MODE", "BooleanMode")]
[StringEnum("WITH QUERY EXPANSION", "QueryExpansion")]
[StringEnum("IN NATURAL LANGUAGE MODE WITH QUERY EXPANSION", "NaturalLanguageModeWithQueryExpansion")]
public sealed partial class MatchAgainstModifier;
