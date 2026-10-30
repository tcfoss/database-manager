using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Postgres.BuiltIn;
using static TcfOss.DatabaseManager.Postgres.BuiltIn.PgDataType;

namespace TcfOss.DatabaseManager.Postgres.Parsing;

public class PgDataTypeParser(Parser parser) : DataTypeParser
{
    public override bool IsDataTypeKeyword(Keyword? keyword) => keyword is
        Keyword.BOOL or Keyword.BOOLEAN or Keyword.INT or Keyword.INTEGER or Keyword.INT4 or
        Keyword.BIGINT or Keyword.INT8 or Keyword.SMALLINT or Keyword.INT2 or
        Keyword.SMALLSERIAL or Keyword.SERIAL2 or Keyword.SERIAL or Keyword.SERIAL4 or Keyword.BIGSERIAL or Keyword.SERIAL8 or
        Keyword.REAL or Keyword.FLOAT4 or Keyword.FLOAT8 or Keyword.FLOAT or Keyword.DOUBLE or Keyword.MONEY or
        Keyword.TEXT or Keyword.BYTEA or Keyword.DATE or Keyword.UUID or Keyword.XML or Keyword.JSON or Keyword.JSONB or Keyword.JSONPATH or
        Keyword.INET or Keyword.CIDR or Keyword.MACADDR or Keyword.MACADDR8 or
        Keyword.POINT or Keyword.LINE or Keyword.LSEG or Keyword.BOX or Keyword.PATH or Keyword.POLYGON or Keyword.CIRCLE or
        Keyword.TSVECTOR or Keyword.TSQUERY or Keyword.PG_LSN or Keyword.PG_SNAPSHOT or Keyword.TXID_SNAPSHOT or
        Keyword.INT4RANGE or Keyword.INT8RANGE or Keyword.NUMRANGE or Keyword.TSRANGE or Keyword.TSTZRANGE or Keyword.DATERANGE or
        Keyword.INT4MULTIRANGE or Keyword.INT8MULTIRANGE or Keyword.NUMMULTIRANGE or Keyword.TSMULTIRANGE or Keyword.TSTZMULTIRANGE or Keyword.DATEMULTIRANGE or
        Keyword.CHAR or Keyword.CHARACTER or Keyword.VARCHAR or Keyword.BPCHAR or Keyword.NUMERIC or Keyword.DECIMAL or
        Keyword.BIT or Keyword.VARBIT or Keyword.TIME or Keyword.TIMETZ or Keyword.TIMESTAMP or Keyword.TIMESTAMPTZ or Keyword.INTERVAL;

    public override DataType ParseDataType(ParserState state)
    {
        Token token = state.Next();
        if (token is not Word word)
        {
            throw ParserState.ExpectedCategoryException("data_type", token);
        }

        return word.Keyword switch
        {
            Keyword.BOOL or Keyword.BOOLEAN => new DataType.Boolean(),
            Keyword.INT or Keyword.INTEGER or Keyword.INT4 => new DataType.Int(),
            Keyword.BIGINT or Keyword.INT8 => new DataType.BigInt(),
            Keyword.SMALLINT or Keyword.INT2 => new PgSmallInt(),
            Keyword.SMALLSERIAL or Keyword.SERIAL2 => new PgSmallSerial(),
            Keyword.SERIAL or Keyword.SERIAL4 => new PgSerial(),
            Keyword.BIGSERIAL or Keyword.SERIAL8 => new PgBigSerial(),
            Keyword.REAL or Keyword.FLOAT4 => new PgReal(),
            Keyword.FLOAT8 => new PgDoublePrecision(),
            Keyword.MONEY => new PgMoney(),
            Keyword.TEXT => new DataType.Text(),
            Keyword.BYTEA => new PgBytea(),
            Keyword.DATE => new PgDate(),
            Keyword.UUID => new PgUuid(),
            Keyword.XML => new PgXml(),
            Keyword.JSON => new PgJson(),
            Keyword.JSONB => new PgJsonb(),
            Keyword.JSONPATH => new PgJsonPath(),
            Keyword.INET => new PgInet(),
            Keyword.CIDR => new PgCidr(),
            Keyword.MACADDR => new PgMacAddr(),
            Keyword.MACADDR8 => new PgMacAddr8(),
            Keyword.POINT => new PgPoint(),
            Keyword.LINE => new PgLine(),
            Keyword.LSEG => new PgLSeg(),
            Keyword.BOX => new PgBox(),
            Keyword.PATH => new PgPath(),
            Keyword.POLYGON => new PgPolygon(),
            Keyword.CIRCLE => new PgCircle(),
            Keyword.TSVECTOR => new PgTsVector(),
            Keyword.TSQUERY => new PgTsQuery(),
            Keyword.PG_LSN => new PgLsn(),
            Keyword.PG_SNAPSHOT => new PgSnapshot(),
            Keyword.TXID_SNAPSHOT => new PgTxidSnapshot(),
            Keyword.INT4RANGE => new PgInt4Range(),
            Keyword.INT8RANGE => new PgInt8Range(),
            Keyword.NUMRANGE => new PgNumRange(),
            Keyword.TSRANGE => new PgTsRange(),
            Keyword.TSTZRANGE => new PgTstzRange(),
            Keyword.DATERANGE => new PgDateRange(),
            Keyword.INT4MULTIRANGE => new PgInt4Multirange(),
            Keyword.INT8MULTIRANGE => new PgInt8Multirange(),
            Keyword.NUMMULTIRANGE => new PgNumMultirange(),
            Keyword.TSMULTIRANGE => new PgTsMultirange(),
            Keyword.TSTZMULTIRANGE => new PgTstzMultirange(),
            Keyword.DATEMULTIRANGE => new PgDateMultirange(),
            Keyword.CHAR or Keyword.CHARACTER => ParseCharacter(state),
            Keyword.BPCHAR => new PgBpchar(ParseOptionalLength(state, min: 1)),
            Keyword.VARCHAR => ParseVarchar(state),
            Keyword.NUMERIC or Keyword.DECIMAL => ParseDecimal(state),
            Keyword.FLOAT => ParseFloat(state),
            Keyword.DOUBLE => ParseDouble(state),
            Keyword.BIT => state.ParseKeyword(Keyword.VARYING) ? new PgVarbit(ParseOptionalLength(state, min: 1)) : new PgBit(ParseOptionalLength(state, min: 1)),
            Keyword.VARBIT => new PgVarbit(ParseOptionalLength(state, min: 1)),
            Keyword.TIME => ParseTemporal(state, false, false),
            Keyword.TIMETZ => ParseTemporal(state, false, true),
            Keyword.TIMESTAMP => ParseTemporal(state, true, false),
            Keyword.TIMESTAMPTZ => ParseTemporal(state, true, true),
            Keyword.INTERVAL => ParseIntervalType(state),
            _ => throw ParserState.ExpectedCategoryException("data_type", token),
        };
    }

    public override DataTypeReference ParseDataTypeReference(ParserState state)
    {
        if (state.Peek() is not Word word)
        {
            throw ParserState.ExpectedCategoryException("data_type", state.Peek());
        }
        if (word.QuoteStyle == QuoteStyle.None && !state.PeekNthIs<Dot>(1) && IsDataTypeKeyword(word.Keyword))
        {
            return base.ParseDataTypeReference(state);
        }

        return new DataTypeReference.Named(parser.ComponentParser.ParseObjectName(state, "data_type"));
    }

    public override DataTypeReference ParseDataTypeReferenceForConvert(ParserState state)
    {
        return ParseDataTypeReference(state);
    }

    protected override DataType ParseChar(ParserState state) => new PgChar(ParseOptionalLength(state, min: 1));
    protected override DataType ParseVarchar(ParserState state) => new PgVarchar(ParseOptionalLength(state, min: 1));

    protected override DataType ParseDecimal(ParserState state)
    {
        if (!state.ConsumeTokenIs<ParenOpen>())
        {
            return new PgNumeric();
        }
        uint precision = ParseBoundedUInt(state, min: 1, max: 1000);
        int? scale = null;
        if (state.ConsumeTokenIs<Comma>())
        {
            scale = ParseBoundedInt(state, min: -1000, max: 1000, label: "scale");
        }
        state.ExpectRightParen();
        return new PgNumeric(precision, scale);
    }

    protected override DataType ParseFloat(ParserState state)
    {
        uint? precision = ParseOptionalSimplePrecision(state, min: 1, max: 53);
        return precision switch
        {
            <= 24 => new PgReal(),
            _ => new PgDoublePrecision(),
        };
    }

    protected override DataType ParseDouble(ParserState state)
    {
        state.ExpectKeyword(Keyword.PRECISION);
        return new PgDoublePrecision();
    }

    private static uint? ParseTemporalPrecision(ParserState state)
    {
        return ParseOptionalSimplePrecision(state, max: 6);
    }

    private static DataType ParseTemporal(ParserState state, bool timestamp, bool timezoneAlias)
    {
        uint? precision = ParseTemporalPrecision(state);
        bool? withTimeZone = timezoneAlias ? true
            : state.ParseKeywordsAll(Keyword.WITH, Keyword.TIME, Keyword.ZONE) ? true
            : state.ParseKeywordsAll(Keyword.WITHOUT, Keyword.TIME, Keyword.ZONE) ? false
            : null;
        return timestamp ? new PgTimestamp(precision, withTimeZone) : new PgTime(precision, withTimeZone);
    }

    private static PgIntervalField? ParseOptionalIntervalField(ParserState state)
    {
        if (state.Peek() is Word word && PgIntervalField.TryParse(word.Keyword.ToString(), out PgIntervalField? field))
        {
            state.Next();
            return field;
        }
        return null;
    }

    private static int IntervalFieldOrder(PgIntervalField field) => field switch
    {
        var value when value == PgIntervalField.Year => 0,
        var value when value == PgIntervalField.Month => 1,
        var value when value == PgIntervalField.Day => 2,
        var value when value == PgIntervalField.Hour => 3,
        var value when value == PgIntervalField.Minute => 4,
        var value when value == PgIntervalField.Second => 5,
        _ => throw new DefensiveGuardException(true, "interval field", field.ToString()),
    };

    private static PgInterval ParseIntervalType(ParserState state)
    {
        PgIntervalField? start = ParseOptionalIntervalField(state);
        PgIntervalField? end = null;
        if (start != null && state.ParseKeyword(Keyword.TO))
        {
            Token endToken = state.Peek();
            end = ParseOptionalIntervalField(state) ?? throw state.ExpectedException("interval field");
            bool valid = start == PgIntervalField.Year && end == PgIntervalField.Month
                || IntervalFieldOrder(start) >= IntervalFieldOrder(PgIntervalField.Day)
                    && IntervalFieldOrder(end) > IntervalFieldOrder(start);
            if (!valid)
            {
                throw new ParseException.InvalidIntervalFieldRange(start.ToString(), end.ToString(), endToken.Location);
            }
        }
        Token precisionToken = state.Peek();
        uint? precision = ParseTemporalPrecision(state);
        if (precision != null && start != null && (end ?? start) != PgIntervalField.Second)
        {
            throw new ParseException.IntervalPrecisionRequiresSecond(precisionToken.Location);
        }
        return new PgInterval(start, end, precision);
    }
}
