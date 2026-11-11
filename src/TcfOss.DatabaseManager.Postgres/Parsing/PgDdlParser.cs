using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Postgres.Parsing;

public class PgDdlParser(Parser parser) : DdlParser(parser)
{
    protected override bool UseSqlRoutineTail(ParserState state) => true;
    protected override bool SupportsQualifiedTriggerNames => false;
    protected override bool SupportsIndexOptionsAfterFilter => false;

    protected override SqlValueList<StatementIndexOption>? ParseOptionalLegacyIndexOptions(ParserState state)
    {
        return null;
    }

    protected override void ValidateCreateIndex(ParserState state, CreateIndex statement)
    {
        if (statement.CreateOrLabel != null || statement is not (CreateIndex.Standard or CreateIndex.Unique))
        {
            throw state.ExpectedException("CREATE [UNIQUE] INDEX");
        }
        (IndexMethod? method, IndexOrganization? organization, IndexStorageLocation? storage, Comment? comment) = statement switch
        {
            CreateIndex.Standard standard => (standard.IndexMethod, standard.IndexOrganization, standard.StorageLocation, standard.Comment),
            CreateIndex.Unique unique => (unique.IndexMethod, unique.IndexOrganization, unique.StorageLocation, unique.Comment),
            _ => throw new InvalidOperationException()
        };
        if (organization != null || storage != null || comment != null
            || method != null && statement.IndexMethodAfterTableName != true)
        {
            throw state.ExpectedException("PostgreSQL index options with USING after the table name");
        }
        foreach (KeyPart key in statement.Columns)
        {
            if (key is KeyPart.IndexExpression indexExpression)
            {
                Expression expression = indexExpression.Expression;
                if (expression is Collate collate)
                {
                    expression = collate.Expression;
                }
                if (expression is not (SingleIdentifier or Nested or FunctionCall or Cast { UsePostgresCastSyntax: false } or Position)
                    || expression is FunctionCall { Over: not null } or FunctionCall { Filter: not null } or FunctionCall { WithinGroup: not null })
                {
                    throw state.ExpectedException("column name, function call, or parenthesized index expression");
                }
            }
        }
    }

    protected override MsViewWithOptions? ParseOptionalViewWithOptions(ParserState state)
    {
        return null;
    }

    protected override bool ParseOptionalViewIfNotExists(ParserState state)
    {
        return false;
    }

    protected override ObjectName? ParseDropIndexOnObject(ParserState state)
    {
        return null;
    }

    protected override ObjectName? ParseDropTriggerOnObject(ParserState state)
    {
        state.ExpectKeyword(Keyword.ON, advance: false);
        return base.ParseDropTriggerOnObject(state);
    }

    protected override bool ParseOptionalDropTemporary(ParserState state)
    {
        return false;
    }

    public override TriggerTime ParseTriggerTime(ParserState state)
    {
        if (state.PeekKeyword(Keyword.FOR))
        {
            throw state.ExpectedException("BEFORE", "AFTER", "INSTEAD OF");
        }
        return base.ParseTriggerTime(state);
    }

    protected override bool ParseOptionalTriggerIfNotExists(ParserState state)
    {
        return false;
    }

    protected override ObjectName? ParseOptionalTriggerOnTableBeforeTime(ParserState state)
    {
        return null;
    }

    protected override TriggerOrder? ParseOptionalTriggerOrder(ParserState state)
    {
        return null;
    }

    protected override bool ParseTriggerBodyStartsWithAs(ParserState state)
    {
        return false;
    }

    protected override List<TriggerEvent>? ParseAdditionalTriggerEventsViaComma(ParserState state)
    {
        return null;
    }

    protected override Statement ParseTriggerBody(ParserState state)
    {
        return ParseTriggerExecute(state);
    }

    protected override RoutineParamsSupportStyle GetRoutineParamsSupportStyle(bool isFunction)
    {
        return isFunction ? RoutineParamsSupportStyle.PostgreSqlFunction : RoutineParamsSupportStyle.PostgreSqlProcedure;
    }
}
