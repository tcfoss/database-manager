using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.MySql.Statements.Components;
using Ksc = TcfOss.DatabaseManager.Core.Parsing.KeywordSearchCondition;

namespace TcfOss.DatabaseManager.MySql.Parsing;

public class MyTableParser(MyParser parser) : TableParser(parser)
{
    private readonly Func<ParserState, string> _parseString = ValueParser.ParseLiteralString;

    protected override StatementTableOption? ParseNextTableOption(ParserState state)
    {
        while (true)
        {
            List<Keyword> keywords;
            if ((keywords = state.ParseKeywordSequence(Ksc.Optional(Keyword.STORAGE), Keyword.ENGINE)).Count != 0)
            {
                bool hasEquals = state.ConsumeTokenIs<Equal>();
                string value = _parseString(state);
                return new MyStatementTableOption.Engine(value)
                {
                    IncludeEquals = hasEquals,
                    IncludeStorage = keywords[0] == Keyword.STORAGE,
                };
            }
            if ((keywords = state.ParseKeywordSequence(Ksc.Optional(Keyword.DEFAULT), Keyword.CHARACTER, Keyword.SET)).Count != 0)
            {
                bool hasEquals = state.ConsumeTokenIs<Equal>();
                string value = _parseString(state);
                return new MyStatementTableOption.CharacterSet(value)
                {
                    IncludeEquals = hasEquals,
                    IncludeDefault = keywords[0] == Keyword.DEFAULT,
                    AsCharset = false,
                };
            }
            if ((keywords = state.ParseKeywordSequence(Ksc.Optional(Keyword.DEFAULT), Keyword.CHARSET)).Count != 0)
            {
                bool hasEquals = state.ConsumeTokenIs<Equal>();
                string value = _parseString(state);
                return new MyStatementTableOption.CharacterSet(value)
                {
                    IncludeEquals = hasEquals,
                    IncludeDefault = keywords[0] == Keyword.DEFAULT,
                    AsCharset = true,
                };
            }
            if ((keywords = state.ParseKeywordSequence(Ksc.Optional(Keyword.DEFAULT), Keyword.COLLATE)).Count != 0)
            {
                bool hasEquals = state.ConsumeTokenIs<Equal>();
                string value = _parseString(state);
                return new MyStatementTableOption.Collation(value)
                {
                    IncludeEquals = hasEquals,
                    IncludeDefault = keywords[0] == Keyword.DEFAULT,
                };
            }
            if (state.ParseKeyword(Keyword.AUTO_INCREMENT))
            {
                bool hasEquals = state.ConsumeTokenIs<Equal>();
                ulong value = ValueParser.ParseLiteralULong(state);
                return new MyStatementTableOption.AutoIncrement(value)
                {
                    IncludeEquals = hasEquals,
                };
            }
            if (state.ParseKeyword(Keyword.COMMENT))
            {
                bool hasEquals = state.ConsumeTokenIs<Equal>();
                string value = _parseString(state);
                return new MyStatementTableOption.Comment(value)
                {
                    IncludeEquals = hasEquals
                };
            }
            return null;
        }
    }

    public override IndexMethod ParseIndexMethod(ParserState state)
    {
        Token token = state.Next();
        return token switch
        {
            Word { Keyword: Keyword.BTREE } => IndexMethod.Btree,
            Word { Keyword: Keyword.HASH } => IndexMethod.Hash,
            Word { Keyword: Keyword.RTREE } => IndexMethod.Rtree,
            _ => throw state.ExpectedException<IndexMethod>(token)
        };
    }
}
