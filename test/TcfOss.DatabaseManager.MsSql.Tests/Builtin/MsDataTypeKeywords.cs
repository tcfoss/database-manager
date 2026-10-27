using System.Collections.Immutable;
using TcfOss.DatabaseManager.Core.BuiltIn;

namespace TcfOss.DatabaseManager.MsSql.Tests.Builtin;

public static class MsDataTypeKeywords
{
    public static readonly ImmutableArray<Keyword> All = [
        Keyword.SMALLINT,
        Keyword.BIT,
        Keyword.MONEY,
        Keyword.SMALLMONEY,
        Keyword.DATE,
        Keyword.TIME,
        Keyword.DATETIME,
        Keyword.SMALLDATETIME,
        Keyword.DATETIME2,
        Keyword.DATETIMEOFFSET,
        Keyword.UNIQUEIDENTIFIER,
        Keyword.ROWVERSION,
        Keyword.TIMESTAMP,
        Keyword.IMAGE,
        Keyword.NTEXT,
        Keyword.NCHAR,
        Keyword.BINARY,
        Keyword.VARBINARY
    ];
}
