using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
// using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.MsSql.BuiltIn;
using TcfOss.DatabaseManager.MsSql.Lexing;
using TcfOss.DatabaseManager.MsSql.Parsing;

namespace TcfOss.DatabaseManager.MsSql.Tests.IO.Formatting;

public static class Helpers
{
    public static Formatter CreateFormatter(
        // PseudoTableSet? pseudoTables,
        FormattingSettings? formatSettings = null,
        bool preferTabs = false,
        bool openingParensOnNewLine = true,
        IdentifierQuotationHandling quoting = IdentifierQuotationHandling.Always,
        bool objectNamePrefixWithSchema = true,
        int? joinConditionIndent = null,
        int? valueListMultiLineThreshold = 3,
        bool selectItemPrefixWithObject = true,
        bool insertUpdateTargetPrefixWithObject = false,
        bool insertUpdateSourcePrefixWithObject = true,
        bool updateTargetPrefixWithObject = true,
        bool updateSourcePrefixWithObject = true,
        bool expandWildcards = false,
        int? routineParameterMultiLineThreshold = 3)
    {
        formatSettings ??= new FormattingSettings()
        {
            ObjectNamePrefixWithSchema = objectNamePrefixWithSchema,
            OmitModifiersIfDefault = true,
            OpeningParensOnNewLine = openingParensOnNewLine,
            PreferTabs = preferTabs,
            TabSize = 4,
            Quoting = quoting,
            JoinConditionIndent = joinConditionIndent,
            ValueListMultiLineThreshold = valueListMultiLineThreshold,
            SelectItemPrefixWithObject = selectItemPrefixWithObject,
            InsertUpdateTargetPrefixWithObject = insertUpdateTargetPrefixWithObject,
            InsertUpdateSourcePrefixWithObject = insertUpdateSourcePrefixWithObject,
            UpdateTargetPrefixWithObject = updateTargetPrefixWithObject,
            UpdateSourcePrefixWithObject = updateSourcePrefixWithObject,
            ExpandWildcards = expandWildcards,
            RoutineParameterMultiLineThreshold = routineParameterMultiLineThreshold,
        };

        var config = TestConfig.GetMsTestConfig(formatSettings: formatSettings);
        var formatter = new Formatter(config, new TextParser(new MsLexer(), new MsParser()), new MsFunctionNameProvider());
        return formatter;
    }
}
