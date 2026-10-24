using TcfOss.DatabaseManager.MariaDb.DatabaseComms.EntityFramework.Entities;

namespace TcfOss.DatabaseManager.MariaDb.Tests.DatabaseComms;

public static class MariaDbExtraData
{
    public static TableEntity[] GetTableEntities()
    {
        return [
            new TableEntity { TableCatalog = "def", TableSchema = "extra_test", TableName = "with_desc_index", TableType = "BASE TABLE", Engine = "InnoDB", RowFormat = "Dynamic", TableRows = 10, AvgRowLength = 100, DataLength = 1000, MaxDataLength = 1000, DataFree = 0, AutoIncrement = 1, CreateTime = DateTime.Now, UpdateTime = DateTime.Now, CheckTime = null, TableCollation = "utf8mb4_general_ci", Checksum = null, CreateOptions = "", TableComment = "", MaxIndexLength = 0, Temporary = "N" },
        ];
    }

    public static ColumnEntity[] GetColumnEntities()
    {
        return [
            new ColumnEntity { TableCatalog = "def", TableSchema = "extra_test", TableName = "with_desc_index", ColumnName = "id", OrdinalPosition = 1, ColumnDefault = null, IsNullable = "NO", DataType = "int", CharacterMaximumLength = null, CharacterOctetLength = null, NumericPrecision = 10, NumericScale = 0, DatetimePrecision = null, CharacterSetName = null, CollationName = null, ColumnType = "int(11)", ColumnKey = "PRI", Extra = "auto_increment", Privileges = "select,insert,update,references", ColumnComment = "", IsGenerated = "NEVER", GenerationExpression = null },
            new ColumnEntity { TableCatalog = "def", TableSchema = "extra_test", TableName = "with_desc_index", ColumnName = "val", OrdinalPosition = 2, ColumnDefault = null, IsNullable = "NO", DataType = "varchar", CharacterMaximumLength = 10, CharacterOctetLength = 40, NumericPrecision = null, NumericScale = null, DatetimePrecision = null, CharacterSetName = "utf8mb4", CollationName = "utf8mb4_general_ci", ColumnType = "varchar(10)", ColumnKey = "MUL", Extra = "", Privileges = "select,insert,update,references", ColumnComment = "", IsGenerated = "NEVER", GenerationExpression = null }
        ];
    }

    public static TableConstraintEntity[] GetTableConstraintEntities()
    {
        return [
            new TableConstraintEntity { ConstraintCatalog = "def", ConstraintSchema = "extra_test", ConstraintName = "PRIMARY", TableSchema = "extra_test", TableName = "with_desc_index", ConstraintType = "PRIMARY KEY" }
        ];
    }

    public static StatisticsEntity[] GetStatisticsEntities()
    {
        return [
            new StatisticsEntity { TableCatalog = "def", TableSchema = "extra_test", TableName = "with_desc_index", NonUnique = 0, IndexSchema = "extra_test", IndexName = "PRIMARY", SeqInIndex = 1, ColumnName = "id", Collation = "D", Cardinality = 0, SubPart = null, Packed = null, Nullable = "", IndexType = "BTREE", Comment = "", IndexComment = "", Ignored = "NO" },
            new StatisticsEntity { TableCatalog = "def", TableSchema = "extra_test", TableName = "with_desc_index", NonUnique = 1, IndexSchema = "extra_test", IndexName = "idx_with_desc_index_val", SeqInIndex = 1, ColumnName = "val", Collation = "D", Cardinality = 0, SubPart = 5, Packed = null, Nullable = "", IndexType = "BTREE", Comment = "", IndexComment = "", Ignored = "NO" }
        ];
    }

    public static KeyColumnUsageEntity[] GetKeyColumnUsageEntities()
    {
        return [
            new KeyColumnUsageEntity { ConstraintCatalog = "def", ConstraintSchema = "extra_test", ConstraintName = "PRIMARY", TableCatalog = "def",TableSchema = "extra_test", TableName = "with_desc_index", ColumnName = "id", OrdinalPosition = 1, PositionInUniqueConstraint = null, ReferencedTableSchema = null, ReferencedTableName = null, ReferencedColumnName = null },
        ];
    }

    public static EventEntity[] GetEventEntities()
    {
        return [
            new EventEntity { EventCatalog = "def", EventSchema = "extra_test", EventName = "event_at", Definer = "admin@localhost", TimeZone = "SYSTEM", EventBody = "SQL", EventDefinition = "BEGIN SELECT 1; END", EventType = "ONE TIME", ExecuteAt = DateTime.Parse("2026-07-04 00:00:00"), IntervalValue = null, IntervalField = null, SqlMode = "STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION", Starts = null, Ends = null, Status = "ENABLED", OnCompletion = "NOT PRESERVE", Created = DateTime.Now, LastAltered = DateTime.Now, LastExecuted = null, EventComment = "", Originator = 1, CharacterSetClient = "utf8mb4", CollationConnection = "utf8mb4_general_ci", DatabaseCollation = "utf8mb4_general_ci" },
            new EventEntity { EventCatalog = "def", EventSchema = "extra_test", EventName = "event_disable", Definer = "admin@localhost", TimeZone = "SYSTEM", EventBody = "SQL", EventDefinition = "BEGIN SELECT 1; END", EventType = "ONE TIME", ExecuteAt = DateTime.Parse("2026-07-04 00:00:00"), IntervalValue = null, IntervalField = null, SqlMode = "STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION", Starts = null, Ends = null, Status = "DISABLED", OnCompletion = "PRESERVE", Created = DateTime.Now, LastAltered = DateTime.Now, LastExecuted = null, EventComment = "", Originator = 1, CharacterSetClient = "utf8mb4", CollationConnection = "utf8mb4_general_ci", DatabaseCollation = "utf8mb4_general_ci" },
            new EventEntity { EventCatalog = "def", EventSchema = "extra_test", EventName = "event_disable_on_slave", Definer = "admin@localhost", TimeZone = "SYSTEM", EventBody = "SQL", EventDefinition = "BEGIN SELECT 1; END", EventType = "ONE TIME", ExecuteAt = DateTime.Parse("2026-07-04 00:00:00"), IntervalValue = null, IntervalField = null, SqlMode = "STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION", Starts = null, Ends = null, Status = "SLAVESIDE_DISABLED", OnCompletion = "PRESERVE", Created = DateTime.Now, LastAltered = DateTime.Now, LastExecuted = null, EventComment = "", Originator = 1, CharacterSetClient = "utf8mb4", CollationConnection = "utf8mb4_general_ci", DatabaseCollation = "utf8mb4_general_ci" }
        ];
    }
}
