using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;
using TcfOss.DatabaseManager.Postgres.BuiltIn;
using TcfOss.DatabaseManager.Postgres.Configuration;
using TcfOss.DatabaseManager.Postgres.DatabaseObjects;
using TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

namespace TcfOss.DatabaseManager.Postgres.DefinitionBuilding;

public class PgCommentResolutionManager(PgConfig config, PgDefinition definition, PgTypeResolutionManager types)
{
    private readonly PgConfig _config = config;
    private readonly PgDefinition _definition = definition;
    private readonly PgTypeResolutionManager _types = types;

    public PgCommentTarget Resolve(PgStatementCommentTarget target, SchemaIdentifier schema, SourceRef source)
    {
        switch (target)
        {
            case PgStatementCommentTarget.DatabaseObject databaseObject:
                return ResolveObject(databaseObject, schema, source);
            case PgStatementCommentTarget.Column column:
                if (column.Name.Values.Count < 2)
                {
                    throw new SqlSyntaxException.SelectIdentifierNotFound(column.Name.ToSql(), source);
                }
                var tableName = new ObjectName([.. column.Name.Values[..^1]]);
                PgTable table = ResolveTable(tableName, schema, source);
                Identifier columnName = column.Name.Values[^1];
                Handle columnHandle = Handle.Create(columnName, _config.NameHandling);
                PgColumn resolved = table.Columns.FirstOrDefault(item => Handle.Create(item.Name, _config.NameHandling) == columnHandle)
                    ?? throw new SqlSyntaxException.SelectIdentifierNotFound(column.Name.ToSql(), source);
                return new PgCommentTarget.Column(table.Name, resolved.Name);
            case PgStatementCommentTarget.Routine routine:
                return ResolveRoutine(routine, schema, source);
            case PgStatementCommentTarget.Trigger trigger:
                PgTable onTable = ResolveTable(trigger.OnTable, schema, source);
                if (_definition.Triggers.TryGetValue(onTable.Name, out DatabaseComponentDict<PgTrigger>? triggers)
                    && triggers.TryGetValue(Handle.Create(trigger.Name, _config.NameHandling), out PgTrigger? resolvedTrigger))
                {
                    return new PgCommentTarget.Trigger(onTable.Name, resolvedTrigger.Name.ToSimpleIdentifier());
                }
                throw new SqlSyntaxException.SelectIdentifierNotFound(trigger.ToSql(), source);
            default:
                throw new SqlSyntaxException.UnsupportedDialectClause("PostgreSQL", "COMMENT ON " + target.GetType().Name, source);
        }
    }

    private PgCommentTarget.DatabaseObject ResolveObject(PgStatementCommentTarget.DatabaseObject target, SchemaIdentifier schema, SourceRef source)
    {
        if (target.ObjectType == PgCommentObjectType.View || target.ObjectType == PgCommentObjectType.Sequence)
        {
            throw new SqlSyntaxException.UnsupportedDialectClause("PostgreSQL", "definition construction for COMMENT ON " + target.ObjectType, source);
        }
        foreach (ObjectHandle candidate in _config.GetCandidateHandles(target.Name, schema))
        {
            if (target.ObjectType == PgCommentObjectType.Table && _definition.Tables.TryGetValue(candidate, out PgTable? table))
            {
                return new PgCommentTarget.DatabaseObject(PgCommentObjectType.Table, table.Name);
            }
            if ((target.ObjectType == PgCommentObjectType.Type || target.ObjectType == PgCommentObjectType.Domain)
                && _definition.CustomTypes.TryGetValue(candidate, out PgCustomType? customType)
                && (target.ObjectType != PgCommentObjectType.Domain || customType is PgCustomType.Domain))
            {
                return new PgCommentTarget.DatabaseObject(PgCommentObjectType.Type, customType.Name);
            }
            if (target.ObjectType == PgCommentObjectType.Type && _definition.Tables.TryGetValue(candidate, out PgTable? rowTable))
            {
                return new PgCommentTarget.DatabaseObject(PgCommentObjectType.Type, rowTable.Name);
            }
            if (target.ObjectType == PgCommentObjectType.Index)
            {
                foreach (PgTable indexedTable in _definition.Tables.Values)
                {
                    foreach (PgIndex index in indexedTable.Indexes.Values)
                    {
                        var indexName = new ObjectIdentifier(index.Name.Name, indexedTable.Name.Schema, index.Name.QuoteStyle);
                        if (ObjectHandle.Create(indexName, _config.NameHandling) == candidate)
                        {
                            return new PgCommentTarget.DatabaseObject(PgCommentObjectType.Index, indexName);
                        }
                    }
                }
            }
        }
        throw new SqlSyntaxException.SelectIdentifierNotFound(target.ToSql(), source);
    }

    private PgCommentTarget.Routine ResolveRoutine(PgStatementCommentTarget.Routine target, SchemaIdentifier schema, SourceRef source)
    {
        SqlValueList<DataType>? requestedTypes = target.Parameters == null ? null :
            [.. target.Parameters.Where(parameter => parameter.Direction != RoutineParameterDirection.Out)
                .Select(parameter => NormalizeIdentityType(_types.Resolve(parameter.DataType, schema, source)))];
        foreach (ObjectHandle candidate in _config.GetCandidateHandles(target.Name, schema))
        {
            ObjectIdentifier name;
            SqlValueList<RoutineParameter.Postgres> parameters;
            if (target.ObjectType == PgCommentObjectType.Function && _definition.Functions.TryGetValue(candidate, out PgStoredFunction? function))
            {
                name = function.Name;
                parameters = function.Parameters;
            }
            else if (target.ObjectType == PgCommentObjectType.Procedure && _definition.Procedures.TryGetValue(candidate, out PgStoredProcedure? procedure))
            {
                name = procedure.Name;
                parameters = procedure.Parameters;
            }
            else
            {
                continue;
            }
            SqlValueList<DataType> argumentTypes = [.. parameters.Where(parameter => parameter.Direction != RoutineParameterDirection.Out)
                .Select(parameter => NormalizeIdentityType(parameter.DataType))];
            if (requestedTypes == null || requestedTypes.SequenceEqual(argumentTypes))
            {
                return new PgCommentTarget.Routine(target.ObjectType, name, argumentTypes);
            }
        }
        throw new SqlSyntaxException.SelectIdentifierNotFound(target.ToSql(), source);
    }

    private static DataType NormalizeIdentityType(DataType dataType)
    {
        return dataType switch
        {
            PgDataType.PgNumeric => new PgDataType.PgNumeric(),
            PgDataType.PgChar or PgDataType.PgBpchar => new PgDataType.PgBpchar(),
            PgDataType.PgVarchar => new PgDataType.PgVarchar(),
            PgDataType.PgBit => new PgDataType.PgBit(),
            PgDataType.PgVarbit => new PgDataType.PgVarbit(),
            PgDataType.PgTime time => new PgDataType.PgTime(null, time.WithTimeZone ?? false),
            PgDataType.PgTimestamp timestamp => new PgDataType.PgTimestamp(null, timestamp.WithTimeZone ?? false),
            PgDataType.PgInterval => new PgDataType.PgInterval(),
            PgDataType.Array array => new PgDataType.Array(NormalizeIdentityType(GetArrayElement(array))),
            _ => dataType
        };
    }

    private static DataType GetArrayElement(PgDataType.Array array)
    {
        DataType element = array.ElementType;
        while (element is PgDataType.Array nested)
        {
            element = nested.ElementType;
        }
        return element;
    }

    private PgTable ResolveTable(ObjectName name, SchemaIdentifier schema, SourceRef source)
    {
        foreach (ObjectHandle candidate in _config.GetCandidateHandles(name, schema))
        {
            if (_definition.Tables.TryGetValue(candidate, out PgTable? table))
            {
                return table;
            }
        }
        throw new SqlSyntaxException.SelectIdentifierNotFound(name.ToSql(), source);
    }
}
