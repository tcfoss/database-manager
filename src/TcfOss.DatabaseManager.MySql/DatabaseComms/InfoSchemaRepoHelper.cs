using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseComms;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.MySql.BuiltIn;
using TcfOss.DatabaseManager.MySql.DatabaseComms.InfoSchemaHelperModels;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;
using TcfOss.DatabaseManager.MySql.DatabaseObjects.Components;
using GenerationMode = TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes.GenerationMode;

namespace TcfOss.DatabaseManager.MySql.DatabaseComms;

public partial class InfoSchemaRepoHelper(ILexer lexer, IParser parser, QuoteStyle quoteStyle)
{
    private readonly Regex _isAggregateRegex = BuildAggregateFunctionRegex();
    private readonly Regex _escapedQuoteRegex = BuildEscapedQuoteRegex();

    private readonly ILexer _lexer = lexer;
    private readonly IParser _parser = parser;
    private readonly QuoteStyle _quoteStyle = quoteStyle;

    private ParserState GetState(string text)
    {
        return new ParserState([.. _lexer.Tokenize(text)]);
    }

    public Expression ParseExpr(string text)
    {
        return _parser.ExpressionParser.ParseExpr(GetState(text));
    }

    public Account ParseAccount(string text)
    {
        ExtendedQuoteStyle exQuoteStyle = _quoteStyle switch
        {
            QuoteStyle.Backticks => ExtendedQuoteStyle.Backticks,
            QuoteStyle.Ansi => ExtendedQuoteStyle.Ansi,
            QuoteStyle.Brackets => ExtendedQuoteStyle.Brackets,
            QuoteStyle.None => ExtendedQuoteStyle.None,
            _ => throw new DefensiveGuardException("InfoSchemaRepoHelper.ParseAccount::parse quote style", _quoteStyle.ToString())
        };

        Account account = ComponentParser.ParseAccount(GetState(text));
        if (account is Account.Identity ai)
        {
            return new Account.Identity(ConvertExtended(ai.Name));
        }
        else if (account is Account.IdentityWithHost ah)
        {
            return new Account.IdentityWithHost(ConvertExtended(ah.Name), ConvertExtended(ah.Host));
        }
        return account;

        ExtendedIdentifier ConvertExtended(ExtendedIdentifier ext)
        {
            return new ExtendedIdentifier(ext.Name, exQuoteStyle);
        }
    }

    public DataType ParseDataType(string text)
    {
        return _parser.DataTypeParser.ParseDataType(GetState(text));
    }

    public DataType ParseDataTypeWithDefaults(string text, string? characterSetName, string? collationName)
    {
        DataType dataType = ParseDataType(text);
        if (dataType is MyDataType.BaseMyStringType { StringAttribute: null } myStringType)
        {
            dataType = myStringType with
            {
                StringAttribute = new StringAttribute(characterSetName, collationName)
            };
        }
        else if (dataType is MyDataType.BaseMyIntegerType myIntegralType && myIntegralType.NumericAttribute == null)
        {
            dataType = myIntegralType with
            {
                NumericAttribute = MySqlNumericAttribute.Signed
            };
        }
        else if (dataType is MyDataType.MyDecimal myDecimal && myDecimal.NumericAttribute == null)
        {
            dataType = myDecimal with
            {
                NumericAttribute = MySqlNumericAttribute.Signed
            };
        }
        else if (dataType is MyDataType.MyDouble myDouble && myDouble.NumericAttribute == null)
        {
            dataType = myDouble with
            {
                NumericAttribute = MySqlNumericAttribute.Signed
            };
        }
        else if (dataType is MyDataType.MyFloat myFloat && myFloat.NumericAttribute == null)
        {
            dataType = myFloat with
            {
                NumericAttribute = MySqlNumericAttribute.Signed
            };
        }
        return dataType;
    }

    public ColumnOption.ColumnDefault? ParseColumnDefault(string? text, bool isNullable)
    {
        if (text == null)
        {
            if (isNullable)
            {
                return new ColumnOption.ColumnDefault.DefaultValue(new Value.Null());
            }
            else
            {
                return null;
            }
        }
        if (text == "")
        {
            return new ColumnOption.ColumnDefault.DefaultValue(new Value.SingleQuotedString(""));
        }

        if (text.StartsWith('(') && text.EndsWith(')'))
        {
            text = text[1..^1].Trim();
        }

        List<Token> tokens = _lexer.Tokenize(text);
        var defState = new ParserState([.. tokens]);

        if (defState.TryParse(ValueParser.ParseValue, out Value? val))
        {
            return new ColumnOption.ColumnDefault.DefaultValue(val);
        }

        Expression defaultExpression = _parser.ExpressionParser.ParseExpr(defState);

        return new ColumnOption.ColumnDefault.DefaultExpression(defaultExpression);

    }

    public ColumnOption.Generated? ParseGenerated(string? text, string? columnExtra, bool removeSlashesBeforeQuotes)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }


        string genText = removeSlashesBeforeQuotes
            ? _escapedQuoteRegex.Replace(text, "'")
            : text;

        Expression genExpr = ParseExpr(genText);

        GenerationMode genMode = columnExtra?.Contains("STORED GENERATED", StringComparison.InvariantCultureIgnoreCase) == true ? GenerationMode.Stored : GenerationMode.Virtual;
        return new ColumnOption.Generated.AsExpression(genExpr, genMode);
    }

    public ColumnOption.CheckConstraint? ParseColumnCheck(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        Expression checkExpr = ParseExpr(text);
        return new ColumnOption.CheckConstraint(checkExpr);
    }

    public ColumnOption.OnUpdate? ParseOnUpdate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (!text.StartsWith("ON UPDATE", StringComparison.InvariantCultureIgnoreCase))
        {
            return null;
        }

        ParserState updateState = GetState(text);
        updateState.ExpectKeywordsAll(Keyword.ON, Keyword.UPDATE);
        Expression onUpdateExpr = _parser.ExpressionParser.ParseExpr(updateState);
        return new ColumnOption.OnUpdate(onUpdateExpr);
    }

    public MyCheck ParseTableCheck(string text, string constraintName)
    {
        Expression checkExpr = ParseExpr(text);
        var id = new Identifier(constraintName, _quoteStyle);

        return new MyCheck(checkExpr, id);
    }

    public bool IsAggregateFunction(string text)
    {
        return _isAggregateRegex.IsMatch(text);
    }

    public Statement ParseStatement(string text)
    {
        ParserState state = GetState(text);
        return _parser.ParseStatement(state);
    }

    public KeyValuePair<Handle, MyKey> IndexInfoToKey(List<IndexInfo> info, NameHandling nameHandling)
    {
        IndexInfo first = info.First();
        var id = new Identifier(first.IndexName, _quoteStyle);
        var keyParts = new SqlValueList<KeyPart>([.. info.OrderBy(s => s.SeqInIndex).Select(s => new KeyPart.Column(s.ColumnName, s.SubPart, s.Collation))]);
        MyKey key;

        Comment? comment = null;
        if (!string.IsNullOrEmpty(first.IndexComment))
        {
            comment = new Comment(first.IndexComment);
        }

        if (first.IndexType == "FULLTEXT")
        {
            key = new MyKey.FullText(keyParts, id)
            {
                Comment = comment
            };
        }
        else if (first.IndexType == "SPATIAL")
        {
            key = new MyKey.Spatial(keyParts, id)
            {
                Comment = comment
            };
        }
        else
        {
            IndexMethod indexMethod = IndexMethod.Parse(first.IndexType);
            key = new MyKey.Standard(keyParts, id)
            {
                IndexMethod = indexMethod,
                Comment = comment
            };
        }

        return new KeyValuePair<Handle, MyKey>(Handle.Create(id, nameHandling), key);
    }

    public RoutineInfo RoutineToRoutineInfo(RoutineDto e)
    {
        bool isDeterministic = e.IsDeterministic.Equals("yes", StringComparison.OrdinalIgnoreCase);
        Comment? comment = null;
        if (!string.IsNullOrEmpty(e.RoutineComment))
        {
            comment = new Comment(e.RoutineComment);
        }
        SecurityContext securityContext = SecurityContext.Parse(e.SecurityType);
        var definer = new Definer(ParseAccount(e.Definer));
        var relation = SqlDataRelation.Parse(e.SqlDataAccess);

        return new RoutineInfo()
        {
            Definer = definer,
            SecurityContext = securityContext,
            IsDeterministic = isDeterministic,
            SqlDataRelation = relation,
            Comment = comment,
        };
    }

    public Dictionary<ObjectIdentifier, MyTrigger> TriggersOnTableToDictionary(IEnumerable<TriggerDto> tableRows, ObjectIdentifier tableId)
    {
        Dictionary<ObjectIdentifier, MyTrigger> triggers = [];

        var isTrigGroups = from t in tableRows
                           group t by new { t.ActionTiming, t.EventManipulation } into g
                           select g;

        foreach (var group in isTrigGroups)
        {
            foreach (TriggerDto isTrig in group)
            {
                var objId = new ObjectIdentifier(isTrig.TriggerName, tableId.Schema, _quoteStyle);

                var trigTime = TriggerTime.Parse(isTrig.ActionTiming);
                var trigEvent = TriggerEvent.Parse(isTrig.EventManipulation);

                Statement body = ParseStatement(isTrig.ActionStatement);

                triggers[objId] = new MyTrigger(objId, tableId, trigTime, trigEvent, body)
                {
                    Definer = new Definer(ParseAccount(isTrig.Definer)),
                    Order = (uint)isTrig.ActionOrder,
                    RawBodyText = isTrig.ActionStatement
                };
            }
        }

        return triggers;
    }

    public static string GetMetadataTableName(SchemaIdentifier schemaId)
    {
        string escapedSchemaName = schemaId.Name.Replace("`", "``").Replace("{", "{{").Replace("}", "}}");
        return $"`{escapedSchemaName}`.`{StoredMetadataConstants.TableName}`";
    }

    public static string GetMetadataTableCreateSql(string fullTableName)
    {
        return $"""
            CREATE TABLE {fullTableName} (
                `{StoredMetadataConstants.UidColumn}` CHAR(36) NOT NULL,
                `{StoredMetadataConstants.TypeColumn}` CHAR(1) NOT NULL,
                UNIQUE KEY `{StoredMetadataConstants.TableName}_entry_key` (`{StoredMetadataConstants.UidColumn}`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
            """;
    }

    public static string GetValuesPlaceholders(int count, bool includeRowKeyword)
    {
        string? row = includeRowKeyword ? "ROW" : null;
        return string.Join(',',
            Enumerable.Range(0, count)
                .Select(i => $"{row}({{{i}}})"));
    }


    public static string GetUnappliedEntriesSql(
        IReadOnlyCollection<SchemaIdentifier> schemaIds,
        int idCount,
        string entryType,
        bool includeRowKeyword)
    {
        var unionBuilder = new StringBuilder();
        foreach (SchemaIdentifier schemaId in schemaIds)
        {
            if (unionBuilder.Length > 0)
            {
                unionBuilder.AppendLine("UNION ALL");
            }
            string fullTableName = GetMetadataTableName(schemaId);
            unionBuilder.AppendLine(CultureInfo.InvariantCulture, $"""
                SELECT `{StoredMetadataConstants.UidColumn}`
                FROM {fullTableName}
                WHERE `{StoredMetadataConstants.TypeColumn}` = '{entryType}'
                """);
        }

        string valuesPlaceholders = GetValuesPlaceholders(idCount, includeRowKeyword);
        return $"""
            VALUES {valuesPlaceholders}
            EXCEPT
            ({unionBuilder})
            """;
    }

    [GeneratedRegex(@"LOOP\s+FETCH\s+GROUP\sNEXT\s+ROW\s+")]
    private static partial Regex BuildAggregateFunctionRegex();

    [GeneratedRegex(@"(?<!\\)\\'")]
    private static partial Regex BuildEscapedQuoteRegex();
}
