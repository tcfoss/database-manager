using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Parsing;

public static class TokenExtensions
{
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

    public static InertOnly ToInertOnly(this NonSqlList nonSql)
    {
        if (nonSql.Count == 0)
        {
            throw new InvalidOperationException("Cannot convert empty non-sql to InertOnly");
        }
        return new InertOnly()
        {
            Meta = new MetaData
            {
                PreNonSql = nonSql.Items,
                Start = nonSql.Start,
                End = nonSql.End
            }
        };
    }
}
