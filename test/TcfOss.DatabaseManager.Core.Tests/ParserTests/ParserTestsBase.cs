using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;

namespace TcfOss.DatabaseManager.Core.Tests.ParserTests;

public class ParserTestsBase<TLexer, TParser>
    where TLexer : ILexer, new()
    where TParser : Parser, new()
{
    protected static Token[] GetTokens(string sql)
    {
        var tokens = new TLexer().Tokenize(sql);
        return [.. tokens];
    }

    protected static ParserState GetState(string sql)
    {
        var tokens = new TLexer().Tokenize(sql);
        return new ParserState([.. tokens]);
    }

    protected static IWriteSql ParseStatement(string sql)
    {
        var state = new ParserState(GetTokens(sql));
        return new TParser().Parse(state, null).First();
    }

    protected static (string? expected, string? actual) GetExpectedActual(Func<ParserState, IWriteSql?> parser, string sql, string? expectedSql = null, bool normalizeExpectedWhitespace = false, bool normalizeActualWhitespace = false)
    {
        var state = new ParserState(GetTokens(sql));
        var parsed = parser(state);

        var actual = parsed?.ToSql();

        if (normalizeActualWhitespace && actual != null)
        {
            actual = actual.NormalizeWhitespace();
        }

        var expected = expectedSql ?? sql;
        if (normalizeExpectedWhitespace)
        {
            expected = expected.NormalizeWhitespace();
        }

        return (expected, actual);
    }

    protected static (string? expected, string? actual) GetExpectedActual<T>(Func<ParserState, EndSubstatements?, SqlValueList<T>> parser, string sql, string? expectedSql = null, bool normalizeExpectedWhitespace = false, bool normalizeActualWhitespace = false)
        where T : IEquatable<T>
    {
        var state = new ParserState(GetTokens(sql));
        var parsed = parser(state, null);

        var actual = parsed.ToSql();

        if (normalizeActualWhitespace)
        {
            actual = actual.NormalizeWhitespace();
        }

        var expected = expectedSql ?? sql;
        if (normalizeExpectedWhitespace)
        {
            expected = expected.NormalizeWhitespace();
        }

        return (expected, actual);
    }

    protected static (string? expected, string? actual) GetExpectedActual<T>(Func<ParserState, SqlValueList<T>> parser, string sql, string? expectedSql = null, bool normalizeExpectedWhitespace = false, bool normalizeActualWhitespace = false)
        where T : IEquatable<T>
    {
        var state = new ParserState(GetTokens(sql));
        var parsed = parser(state);

        var actual = parsed.ToSql();
        if (normalizeActualWhitespace)
        {
            actual = actual.NormalizeWhitespace();
        }

        var expected = expectedSql ?? sql;
        if (normalizeExpectedWhitespace)
        {
            expected = expected.NormalizeWhitespace();
        }

        return (expected, actual);
    }

    protected static (string? expected, string? actual) GetExpectedActual(string sql, string? expectedSql = null, bool normalizeExpectedWhitespace = false, bool normalizeActualWhitespace = false)
    {
        var state = new ParserState(GetTokens(sql));
        var parsed = new TParser().Parse(state, null).First();

        var actual = parsed.ToSql();
        if (normalizeActualWhitespace)
        {
            actual = actual.NormalizeWhitespace();
        }

        var expected = expectedSql ?? sql;
        if (normalizeExpectedWhitespace)
        {
            expected = expected.NormalizeWhitespace();
        }

        return (expected, actual);
    }
}
