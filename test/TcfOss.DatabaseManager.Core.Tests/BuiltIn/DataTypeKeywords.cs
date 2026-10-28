using System.Collections.Immutable;
using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.Core.Tests.BuiltIn;

public static class DataTypeKeywords
{
    public static readonly ImmutableArray<Keyword> All = [
        Keyword.BOOLEAN,
        Keyword.BOOL,
        Keyword.INT,
        Keyword.INTEGER,
        Keyword.TINYINT,
        Keyword.BIGINT,
        Keyword.DECIMAL,
        Keyword.NUMERIC,
        Keyword.CHAR,
        Keyword.VARCHAR,
        Keyword.NVARCHAR,
        Keyword.TEXT,
        Keyword.DOUBLE,
        Keyword.FLOAT,
        Keyword.ENUM,
        Keyword.SET
    ];
}
