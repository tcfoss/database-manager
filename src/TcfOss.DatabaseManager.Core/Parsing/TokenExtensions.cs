using System.Diagnostics;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;
using NonSqlStatement = TcfOss.DatabaseManager.Core.Statements.Components.NonSql;
using NonSqlToken = TcfOss.DatabaseManager.Core.Lexing.Tokens.NonSql;

namespace TcfOss.DatabaseManager.Core.Parsing;

public static class TokenExtensions
{
    public static BinaryOperator? ToBinaryOperator(this Token token)
    {
        BinaryOperator? op = token switch
        {
            Equal => BinaryOperator.Equal,
            NotEqual => BinaryOperator.NotEqual,
            GreaterThan => BinaryOperator.GreaterThan,
            GreaterThanOrEqual => BinaryOperator.GreaterThanOrEqual,
            LessThan => BinaryOperator.LessThan,
            LessThanOrEqual => BinaryOperator.LessThanOrEqual,
            Plus => BinaryOperator.Add,
            Minus => BinaryOperator.Subtract,
            Asterisk => BinaryOperator.Multiply,
            Slash => BinaryOperator.Divide,
            Modulo => BinaryOperator.Modulo,
            //StringConcat
            //Pipe,
            //Caret
            //Ampersand,
            // Lots of others
            Word { Keyword: Keyword.AND } => BinaryOperator.And,
            Word { Keyword: Keyword.OR } => BinaryOperator.Or,
            Word { Keyword: Keyword.XOR } => BinaryOperator.Xor,
            _ => null
        };

        return op;
    }

    public static DateTimeUnit? ToDateTimeUnit(this Token token)
    {
        if (token is not Word w)
        {
            return null;
        }
        Keyword keyword = w.Keyword;

#pragma warning disable IDE0072 // Add missing cases
        return keyword switch
        {
            Keyword.MICROSECOND => DateTimeUnit.Microsecond,
            Keyword.SECOND => DateTimeUnit.Second,
            Keyword.MINUTE => DateTimeUnit.Minute,
            Keyword.HOUR => DateTimeUnit.Hour,
            Keyword.DAY => DateTimeUnit.Day,
            Keyword.WEEK => DateTimeUnit.Week,
            Keyword.MONTH => DateTimeUnit.Month,
            Keyword.QUARTER => DateTimeUnit.Quarter,
            Keyword.YEAR => DateTimeUnit.Year,
            Keyword.SECOND_MICROSECOND => DateTimeUnit.SecondMicrosecond,
            Keyword.MINUTE_MICROSECOND => DateTimeUnit.MinuteMicrosecond,
            Keyword.MINUTE_SECOND => DateTimeUnit.MinuteSecond,
            Keyword.HOUR_MICROSECOND => DateTimeUnit.HourMicrosecond,
            Keyword.HOUR_SECOND => DateTimeUnit.HourSecond,
            Keyword.HOUR_MINUTE => DateTimeUnit.HourMinute,
            Keyword.DAY_MICROSECOND => DateTimeUnit.DayMicrosecond,
            Keyword.DAY_SECOND => DateTimeUnit.DaySecond,
            Keyword.DAY_MINUTE => DateTimeUnit.DayMinute,
            Keyword.DAY_HOUR => DateTimeUnit.DayHour,
            Keyword.YEAR_MONTH => DateTimeUnit.YearMonth,
            _ => null
        };
#pragma warning restore IDE0072 // Add missing cases

    }

    public static List<NonSqlStatement> ToStatementNonSql(this IList<NonSqlToken> nonSql)
    {
        var result = new List<NonSqlStatement>();

        int i = 0;

        while (i < nonSql.Count)
        {
            NonSqlToken curr = nonSql[i];
            switch (curr.NonSqlType)
            {
                case NonSqlType.Space:
                    GlobWhitespace(NonSqlType.Space, (n) => new NonSqlStatement.Spaces(n));
                    break;
                case NonSqlType.Tab:
                    GlobWhitespace(NonSqlType.Tab, (n) => new NonSqlStatement.Tabs(n));
                    break;
                case NonSqlType.Newline:
                    GlobWhitespace(NonSqlType.Newline, (n) => new NonSqlStatement.Newlines(n));
                    break;
                case NonSqlType.BlockComment:
                    result.Add(new NonSqlStatement.BlockComment(curr.Value!));
                    i++;
                    break;
                case NonSqlType.InlineComment:
                    if (curr.Prefix == null)
                    {
                        throw new InvalidOperationException("Line comment requires a prefix");
                    }
                    result.Add(new NonSqlStatement.LineComment(curr.Value!, curr.Prefix));
                    i++;
                    break;

                default:
                    throw new UnreachableException();
            }
        }

        return result;

        void GlobWhitespace<T>(NonSqlType nonSqlType, Func<int, T> ctor) where T : NonSqlStatement.Whitespace
        {
            int count = 0;
            while (i < nonSql.Count && nonSql[i].NonSqlType == nonSqlType)
            {
                count++;
                i++;
            }
            result.Add(ctor(count));
        }
    }

    public static InertOnly ToInertOnly(this IList<NonSqlToken> nonSql)
    {
        if (nonSql.Count == 0)
        {
            throw new InvalidOperationException("Cannot convert empty non-sql to InertOnly");
        }
        NonSqlToken first = nonSql.First();
        NonSqlToken last = nonSql.Last();
        return new InertOnly()
        {
            Meta = new MetaData
            {
                PreNonSql = nonSql.ToStatementNonSql(),
                Start = first.Location.Position,
                End = last.Location.Position + last.Length
            }
        };
    }
}
