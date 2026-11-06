using System.Globalization;
using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;
using TcfOss.DatabaseManager.Postgres.Configuration;
using TcfOss.DatabaseManager.Postgres.DatabaseObjects;
using TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

namespace TcfOss.DatabaseManager.Postgres.DefinitionBuilding;

public class PgTriggerResolutionManager(PgConfig config, ComponentNormalizer normalizer,
    DatabaseObjectDict<PgTable> tables, DatabaseObjectDict<PgStoredFunction> functions)
{
    private readonly PgConfig _config = config;
    private readonly ComponentNormalizer _normalizer = normalizer;
    private readonly DatabaseObjectDict<PgTable> _tables = tables;
    private readonly DatabaseObjectDict<PgStoredFunction> _functions = functions;

    public PgTrigger Resolve(CreateTrigger statement, SchemaIdentifier activeSchema, SourceRef source)
    {
        PgTable table = ResolveTable(statement.TableName, activeSchema, source);
        ObjectIdentifier name = _normalizer.NormalizeObjectIdentifier(new ObjectIdentifier(statement.Name.Values[0].Name,
            table.Name.Schema, statement.Name.Values[0].QuoteStyle));

        SqlSyntaxException.InvalidTrigger Invalid(string clause)
        {
            return new SqlSyntaxException.InvalidTrigger(name, clause, source);
        }

        if (statement.Name.Values.Count != 1 || statement.IfNotExists || statement.Definer != null
            || statement.Order != null || statement.TableNameBeforeTimeAndEvent || statement.TriggerBodyStartsWithAs)
        {
            throw Invalid("non-PostgreSQL trigger syntax");
        }

        if (statement.Body is not PgTriggerExecute execute)
        {
            throw new SqlSyntaxException.InvalidTrigger(name, "body other than EXECUTE FUNCTION or PROCEDURE", source);
        }

        PgStoredFunction function = ResolveFunction(execute.FunctionName, activeSchema, source);
        if (function.Parameters.Count != 0 || function.ReturnType is not FunctionReturn.PseudoType returnType || returnType.Type != PgPseudoType.Trigger)
        {
            throw Invalid("a function with parameters or a return type other than trigger");
        }

        TriggerForEachType quantifier = statement.ExecutionQuantifier switch
        {
            null => TriggerForEachType.Statement,
            TriggerExecutionQuantifier.ForEach each => each.Type,
            _ => throw new SqlSyntaxException.InvalidTrigger(name, "execution quantifier", source)
        };

        if (statement.TriggerTime != TriggerTime.Before && statement.TriggerTime != TriggerTime.After)
        {
            throw Invalid("timing other than BEFORE or AFTER on a table");
        }
        if (statement.CreateOrLabel != null && statement.CreateOrLabel != CreateOrLabel.OrReplace)
        {
            throw Invalid("OR ALTER");
        }

        var eventTypes = new HashSet<Type>();
        SqlValueList<TriggerEvent> events = [];
        foreach (TriggerEvent triggerEvent in statement.Events)
        {
            if (!eventTypes.Add(triggerEvent.GetType()))
            {
                throw new SqlSyntaxException.SpecifiedMoreThanOnce("trigger event", "trigger", name, source);
            }
            switch (triggerEvent)
            {
                case TriggerEvent.Update update:
                    SqlValueList<Identifier>? columns = null;
                    if (update.Columns != null)
                    {
                        if (update.Columns.Count == 0)
                        {
                            throw Invalid("empty UPDATE OF");
                        }
                        columns = [.. update.Columns.Select(_normalizer.NormalizeSingleIdentifier).OrderBy(column => column.Name, StringComparer.Ordinal)];
                        var columnNames = new HashSet<string>();
                        foreach (Identifier column in columns)
                        {
                            if (!columnNames.Add(column.Name))
                            {
                                throw new SqlSyntaxException.SpecifiedMoreThanOnce(column.Name, "trigger UPDATE OF", name, source);
                            }
                            if (!table.Columns.Any(tableColumn => tableColumn.Name.Name == column.Name))
                            {
                                throw Invalid("UPDATE OF " + column.Name);
                            }
                        }
                    }
                    events.Add(new TriggerEvent.Update(columns));
                    break;
                case TriggerEvent.Truncate:
                    if (quantifier != TriggerForEachType.Statement)
                    {
                        throw Invalid("TRUNCATE FOR ROW");
                    }
                    events.Add(triggerEvent);
                    break;
                case TriggerEvent.Insert or TriggerEvent.Delete:
                    events.Add(triggerEvent);
                    break;
                default:
                    throw Invalid("unknown trigger event");
            }
        }

        if (events.Count == 0)
        {
            throw Invalid("empty event list");
        }

        events = [.. events.OrderBy(triggerEvent => triggerEvent switch
        {
            TriggerEvent.Insert => 0,
            TriggerEvent.Update => 1,
            TriggerEvent.Delete => 2,
            _ => 3
        })];

        PgTriggerConstraintOptions? constraint = null;
        if (statement.PgOptions?.ConstraintOptions is { } constraintStatement)
        {
            if (statement.TriggerTime != TriggerTime.After || quantifier != TriggerForEachType.Row || statement.CreateOrLabel != null)
            {
                throw Invalid("CONSTRAINT without AFTER ROW, or with OR REPLACE");
            }
            bool initiallyDeferred = constraintStatement.InitiallyDeferred ?? false;
            bool deferrable = constraintStatement.Deferrable ?? initiallyDeferred;
            if (!deferrable && initiallyDeferred)
            {
                throw Invalid("NOT DEFERRABLE INITIALLY DEFERRED");
            }
            constraint = new PgTriggerConstraintOptions(constraintStatement.FromTable != null
                ? ResolveTable(constraintStatement.FromTable, activeSchema, source).Name : null, deferrable, initiallyDeferred);
        }

        Identifier? oldTable = null;
        Identifier? newTable = null;
        if (statement.PgOptions?.TransitionRelations is { } relations)
        {
            if (relations.Count == 0 || statement.TriggerTime != TriggerTime.After || constraint != null || events.Count != 1
                || events.OfType<TriggerEvent.Update>().Any(update => update.Columns != null))
            {
                throw Invalid("REFERENCING with incompatible timing, events, UPDATE OF or CONSTRAINT");
            }
            foreach (PgTriggerTransitionRelation relation in relations)
            {
                Identifier relationName = _normalizer.NormalizeSingleIdentifier(relation.Name);
                if (relation.IsNew)
                {
                    if (newTable != null || !events.Any(triggerEvent => triggerEvent is TriggerEvent.Insert or TriggerEvent.Update))
                    {
                        throw Invalid("NEW TABLE");
                    }
                    newTable = relationName;
                }
                else
                {
                    if (oldTable != null || !events.Any(triggerEvent => triggerEvent is TriggerEvent.Delete or TriggerEvent.Update))
                    {
                        throw Invalid("OLD TABLE");
                    }
                    oldTable = relationName;
                }
            }
            if (oldTable?.Name == newTable?.Name)
            {
                throw Invalid("identical OLD TABLE and NEW TABLE names");
            }
        }

        return new PgTrigger(name, table.Name, statement.TriggerTime, events, function.Name,
            [.. execute.Arguments.Select(argument => ResolveArgument(argument, name, source))])
        {
            ForEachType = quantifier,
            ConstraintOptions = constraint,
            OldTransitionTable = oldTable,
            NewTransitionTable = newTable,
            When = statement.PgOptions?.When is { } when
                ? new PgTriggerWhenNormalizer(_config, _normalizer, table, name, events, quantifier, statement.TriggerTime, source)
                    .NormalizeExpressionNoFlatten(when) : null
        };
    }

    private string ResolveArgument(PgStatementTriggerArgument argument, ObjectIdentifier name, SourceRef source)
    {
        return argument switch
        {
            PgStatementTriggerArgument.Named named => _normalizer.NormalizeSingleIdentifier(named.Name).Name,
            PgStatementTriggerArgument.Literal { Value: Value.SingleQuotedString text } => text.Value.Replace("''", "'", StringComparison.Ordinal),
            PgStatementTriggerArgument.Literal { Value: Value.EscapeString text } => text.Decode(),
            PgStatementTriggerArgument.Literal { Value: Value.DollarQuotedString text } => text.Value,
            PgStatementTriggerArgument.Literal { Value: Value.Number { IsLong: false } number } =>
                        int.TryParse(number.Value, NumberStyles.None, CultureInfo.InvariantCulture, out int integer)
                            ? integer.ToString(CultureInfo.InvariantCulture)
                            : number.Value,
            _ => throw new SqlSyntaxException.InvalidTrigger(name, "trigger argument", source)
        };
    }

    private PgTable ResolveTable(ObjectName name, SchemaIdentifier schema, SourceRef source)
    {
        foreach (ObjectHandle candidate in _config.GetCandidateHandles(name, schema))
        {
            if (_tables.TryGetValue(candidate, out PgTable? table))
            {
                return table;
            }
        }
        throw new SqlSyntaxException.TriggerRelationNotFound(name, name.Source is { } location ? location with { SourceId = source.SourceId } : source);
    }

    private PgStoredFunction ResolveFunction(ObjectName name, SchemaIdentifier schema, SourceRef source)
    {
        foreach (ObjectHandle candidate in _config.GetCandidateHandles(name, schema))
        {
            if (_functions.TryGetValue(candidate, out PgStoredFunction? function))
            {
                return function;
            }
        }
        throw new SqlSyntaxException.TriggerFunctionNotFound(name, name.Source is { } location ? location with { SourceId = source.SourceId } : source);
    }
}
