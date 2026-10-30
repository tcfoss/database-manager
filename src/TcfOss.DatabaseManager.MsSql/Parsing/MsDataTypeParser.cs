using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.MsSql.BuiltIn;

namespace TcfOss.DatabaseManager.MsSql.Parsing;

public class MsDataTypeParser : DataTypeParser
{
    protected static MsCharLength? ParseOptionalCharLength(ParserState state, uint min = 0, uint max = uint.MaxValue)
    {
        if (!state.ConsumeTokenIs<ParenOpen>())
        {
            return null;
        }
        if (state.ParseKeyword(Keyword.MAX))
        {
            var charLength = new MsCharLength.Max();
            state.ExpectRightParen();
            return charLength;
        }

        uint length = ParseBoundedUInt(state, min, max, "length");
        state.ExpectRightParen();
        return new MsCharLength.Specified(length);
    }

    protected static MsCharLength ParseCharLength(ParserState state)
    {
        if (!state.ConsumeTokenIs<ParenOpen>())
        {
            throw state.ExpectedCategoryException("data_type_length");
        }

        if (state.ParseKeyword(Keyword.MAX))
        {
            var charLength = new MsCharLength.Max();
            state.ExpectRightParen();
            return charLength;
        }

        uint length = ParseBoundedUInt(state, 1, uint.MaxValue, "length");
        state.ExpectRightParen();
        return new MsCharLength.Specified(length);
    }

    protected override DataType ParseChar(ParserState state)
    {
        MsCharLength length = ParseCharLength(state);
        return new MsDataType.MsChar(length);
    }

    protected override DataType ParseVarchar(ParserState state)
    {
        MsCharLength length = ParseCharLength(state);
        return new MsDataType.MsVarchar(length);
    }

    protected override DataType ParseNationalVarchar(ParserState state)
    {
        MsCharLength length = ParseCharLength(state);
        return new MsDataType.MsNVarchar(length);
    }

    public override bool IsDataTypeKeyword(Keyword? keyword) => keyword is
        Keyword.SMALLINT or Keyword.BIT or Keyword.MONEY or Keyword.SMALLMONEY or
        Keyword.DATE or Keyword.TIME or Keyword.DATETIME or Keyword.SMALLDATETIME or
        Keyword.DATETIME2 or Keyword.DATETIMEOFFSET or
        Keyword.UNIQUEIDENTIFIER or Keyword.ROWVERSION or Keyword.TIMESTAMP or
        Keyword.IMAGE or Keyword.NTEXT or Keyword.NCHAR or
        Keyword.BINARY or Keyword.VARBINARY
        || base.IsDataTypeKeyword(keyword);

    public override DataType ParseDataType(ParserState state)
    {
        Token token = state.Peek();
        Word word = token as Word ?? throw ParserState.ExpectedCategoryException("data_type", token);

        switch (word.Keyword)
        {
            case Keyword.SMALLINT:
                state.Next();
                return new MsDataType.MsSmallInt();
            case Keyword.BIT:
                state.Next();
                return new MsDataType.MsBit();
            case Keyword.MONEY:
                state.Next();
                return new MsDataType.MsMoney();
            case Keyword.SMALLMONEY:
                state.Next();
                return new MsDataType.MsSmallMoney();
            case Keyword.DATE:
                state.Next();
                return new MsDataType.MsDate();
            case Keyword.TIME:
                state.Next();
                return new MsDataType.MsTime(ParseOptionalSimplePrecision(state));
            case Keyword.DATETIME:
                state.Next();
                return new MsDataType.MsDateTime();
            case Keyword.SMALLDATETIME:
                state.Next();
                return new MsDataType.MsSmallDateTime();
            case Keyword.DATETIME2:
                state.Next();
                return new MsDataType.MsDateTime2(ParseOptionalSimplePrecision(state));
            case Keyword.DATETIMEOFFSET:
                state.Next();
                return new MsDataType.MsDateTimeOffset(ParseOptionalSimplePrecision(state));
            case Keyword.UNIQUEIDENTIFIER:
                state.Next();
                return new MsDataType.MsUniqueIdentifier();
            // In T-SQL, TIMESTAMP is a deprecated synonym for ROWVERSION.
            case Keyword.ROWVERSION:
            case Keyword.TIMESTAMP:
                state.Next();
                return new MsDataType.MsRowVersion();
            case Keyword.IMAGE:
                state.Next();
                return new MsDataType.MsImage();
            case Keyword.NTEXT:
                state.Next();
                return new MsDataType.MsNText();
            case Keyword.NCHAR:
                state.Next();
                return new MsDataType.MsNChar(
                    ParseOptionalCharLength(state, min: 1)
                    ?? throw state.ExpectedCategoryException("data_type_length"));
            case Keyword.BINARY:
                state.Next();
                return new MsDataType.MsBinary(
                    ParseOptionalCharLength(state, min: 1)
                    ?? throw state.ExpectedCategoryException("data_type_length"));
            case Keyword.VARBINARY:
                state.Next();
                return new MsDataType.MsVarBinary(
                    ParseOptionalCharLength(state, min: 1)
                    ?? throw state.ExpectedCategoryException("data_type_length"));
            default:
                return base.ParseDataType(state);
        }
    }
}
