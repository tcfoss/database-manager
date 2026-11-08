using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Expressions.Components;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;
using TcfOss.DatabaseManager.Postgres.Configuration;
using TcfOss.DatabaseManager.Postgres.DatabaseObjects;
using TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

namespace TcfOss.DatabaseManager.Postgres.DefinitionBuilding;

public class PgTriggerWhenNormalizer(PgConfig config, ComponentNormalizer componentNormalizer, PgTable table,
    ObjectIdentifier trigger, SqlValueList<TriggerEvent> events, TriggerForEachType quantifier, TriggerTime time, SourceRef source)
    : Normalizer(config, componentNormalizer)
{
    protected override Expression NormalizeIdentifier(SingleIdentifier identifier, bool inSelect = false)
    {
        ValidateRow(identifier.Identifier, null);
        return base.NormalizeIdentifier(identifier, inSelect);
    }

    protected override Expression NormalizeIdentifier(CompoundIdentifier identifier, bool inSelect = false)
    {
        if (identifier.Identifiers.Count != 2)
        {
            throw Invalid("qualified row reference");
        }
        ValidateRow(identifier.Identifiers[0], identifier.Identifiers[1]);
        return base.NormalizeIdentifier(identifier, inSelect);
    }

    protected override Expression NormalizeQualifiedWildcard(QualifiedWildcard wildcard)
    {
        if (wildcard.Qualifier.Values.Count != 1)
        {
            throw Invalid("qualified row reference");
        }
        ValidateRow(wildcard.Qualifier.Values[0], null);
        return base.NormalizeQualifiedWildcard(wildcard);
    }

    protected override Exists NormalizeExists(Exists exists, bool flatten = false)
    {
        throw Invalid("subquery");
    }

    protected override InSubquery NormalizeInSubquery(InSubquery inSubquery)
    {
        throw Invalid("subquery");
    }

    protected override Subquery NormalizeSubquery(Subquery subquery, bool flatten = false)
    {
        throw Invalid("subquery");
    }

    protected override FunctionCall NormalizeFunctionCall(FunctionCall functionCall, bool inSelect = false, bool flatten = false)
    {
        if (functionCall.Over != null || functionCall.Arguments is not FunctionArguments.List { DuplicateTreatment: null, Clauses: null })
        {
            throw Invalid("aggregate or window-function syntax");
        }
        return base.NormalizeFunctionCall(functionCall, inSelect, flatten);
    }

    protected override FunctionArgumentExpression NormalizeFunctionArgumentExpression(FunctionArgumentExpression expression,
        bool inSelect = false, bool flatten = false)
    {
        if (expression is FunctionArgumentExpression.QualifiedWildcard row)
        {
            return new FunctionArgumentExpression.FunctionExpression(NormalizeQualifiedWildcard(new QualifiedWildcard(row.Name)));
        }
        if (expression is not FunctionArgumentExpression.FunctionExpression)
        {
            throw Invalid("wildcard function argument");
        }
        return base.NormalizeFunctionArgumentExpression(expression, inSelect, flatten);
    }

    protected override Expression NormalizeOtherExpression(Expression expression)
    {
        throw Invalid("unsupported expression " + expression.GetType().Name);
    }

    private void ValidateRow(Identifier identifier, Identifier? column)
    {
        string row = ComponentNormalizer.NormalizeSingleIdentifier(identifier).Name;
        bool isNew = row == "new";
        if (row is not ("old" or "new") || quantifier != TriggerForEachType.Row
            || (isNew && events.Any(triggerEvent => triggerEvent is TriggerEvent.Delete or TriggerEvent.Truncate))
            || (!isNew && events.Any(triggerEvent => triggerEvent is TriggerEvent.Insert or TriggerEvent.Truncate)))
        {
            throw Invalid(identifier.Name);
        }
        if (column == null)
        {
            if (isNew && time == TriggerTime.Before && table.Columns.Any(tableColumn => tableColumn.Generated is ColumnOption.Generated.AsExpression))
            {
                throw Invalid("NEW containing generated columns before generation");
            }
            return;
        }
        string columnName = ComponentNormalizer.NormalizeSingleIdentifier(column).Name;
        PgColumn? resolved = table.Columns.FirstOrDefault(tableColumn => tableColumn.Name.Name == columnName);
        bool systemColumn = columnName is "ctid" or "tableoid" or "xmin" or "xmax" or "cmin" or "cmax";
        if (resolved == null && !systemColumn)
        {
            throw Invalid(identifier.Name + "." + column.Name);
        }
        if (isNew && time == TriggerTime.Before && (systemColumn || resolved!.Generated is ColumnOption.Generated.AsExpression))
        {
            throw Invalid("NEW." + column.Name + " before generation");
        }
    }

    private SqlSyntaxException.InvalidTrigger Invalid(string clause)
    {
        return new SqlSyntaxException.InvalidTrigger(trigger, "WHEN " + clause, source);
    }
}
