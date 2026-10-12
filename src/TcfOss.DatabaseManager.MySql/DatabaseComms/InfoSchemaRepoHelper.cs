using System.Diagnostics;
using System.Text.RegularExpressions;
using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.MySql.BuiltIn;
using TcfOss.DatabaseManager.MySql.DatabaseComms.InfoSchemaHelperModels;
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
            _ => throw new UnreachableException()
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


    [GeneratedRegex(@"LOOP\s+FETCH\s+GROUP\sNEXT\s+ROW\s+")]
    private static partial Regex BuildAggregateFunctionRegex();

    [GeneratedRegex(@"(?<!\\)\\'")]
    private static partial Regex BuildEscapedQuoteRegex();
}
