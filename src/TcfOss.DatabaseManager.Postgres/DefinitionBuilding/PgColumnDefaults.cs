using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Postgres.BuiltIn;

namespace TcfOss.DatabaseManager.Postgres.DefinitionBuilding;

public static class PgColumnDefaults
{
    public static PgColumnStorageMode? GetStorage(DataType type)
    {
        return type switch
        {
            PgDataType.Custom or PgDataType.PgTypeOf or PgDataType.PseudoType => null,
            PgDataType.PgNumeric or PgDataType.PgInet or PgDataType.PgCidr => PgColumnStorageMode.Main,
            DataType.Text or PgDataType.PgLengthType or PgDataType.PgBytea or PgDataType.PgXml
                or PgDataType.PgJson or PgDataType.PgJsonb or PgDataType.PgJsonPath
                or PgDataType.PgPath or PgDataType.PgPolygon or PgDataType.PgTsVector or PgDataType.PgTsQuery
                or PgDataType.PgSnapshot or PgDataType.PgTxidSnapshot or PgDataType.Array => PgColumnStorageMode.Extended,
            _ when type.Class is DataTypeClass.Range or DataTypeClass.Multirange => PgColumnStorageMode.Extended,
            _ => PgColumnStorageMode.Plain
        };
    }

    public static ObjectName? GetCollation(DataType type, ObjectName? explicitCollation = null)
    {
        if (explicitCollation != null
            && !(explicitCollation.Values.Count == 1 && explicitCollation.Values[0].Name == "default"))
        {
            return explicitCollation;
        }
        if (explicitCollation != null)
        {
            return new ObjectName([new Identifier("pg_catalog"), new Identifier("default")]);
        }
        if (type is PgDataType.Array array)
        {
            return GetCollation(array.ElementType);
        }
        return type.Class == DataTypeClass.String ? new ObjectName([new Identifier("pg_catalog"), new Identifier("default")]) : null;
    }
}
