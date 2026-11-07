using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.StatementAnalysis;

/// <summary>
/// The principal purpose of this class is to normalize CREATE VIEW SELECT statements to
/// match what is stored in INFORMATION_SCHEMA.VIEWS.
///
/// The harder-to-maintain switch statements were chosen over polymorphism to keep the
/// Expressions and Statements more or less dialect-agnostic.
/// </summary>
/// <param name="config"></param>
public class Normalizer(ConfigBase config, ComponentNormalizer componentNormalizer) : INormalizeSql
{
    protected ConfigBase Config { get; } = config;
    private readonly QuoteStyle _quoteStyle = config.QuoteStyle;
    private readonly NormalizationSettings _normalization = config.NormalizationSettings;
    protected ComponentNormalizer ComponentNormalizer { get; } = componentNormalizer;

    private ObjectIdentifier? _name;
    private PseudoTableSet? _pseudoTables;

    private SchemaIdentifier? _activeSchema;
    private SourceRef? _sourceRef;

    protected virtual INormalizeSql ConstructNewNormalizer()
    {
        return new Normalizer(Config, ComponentNormalizer);
    }

    protected virtual FunctionArgumentExpression NormalizeFunctionArgumentExpression(FunctionArgumentExpression expression, bool inSelect = false, bool flatten = false)
    {
        return expression switch
        {
            FunctionArgumentExpression.Wildcard => expression,
            FunctionArgumentExpression.QualifiedWildcard qw => new FunctionArgumentExpression.QualifiedWildcard(ComponentNormalizer.NormalizeObjectName(qw.Name)),
            FunctionArgumentExpression.FunctionExpression fe => new FunctionArgumentExpression.FunctionExpression(NormalizeExpression(fe.Expression, inSelect: inSelect, flatten: flatten)),
            _ => throw new DefensiveGuardException(true, "function argument expression type", expression.GetType())
        };
    }

    private FunctionArgument NormalizeFunctionArgument(FunctionArgument arg, bool inSelect = false, bool flatten = false)
    {
        return arg switch
        {
            FunctionArgument.Unnamed un => new FunctionArgument.Unnamed(NormalizeFunctionArgumentExpression(un.Argument, inSelect: inSelect, flatten: flatten)),
            FunctionArgument.Named named => new FunctionArgument.Named(ComponentNormalizer.NormalizeSingleIdentifier(named.Name), NormalizeFunctionArgumentExpression(named.Argument, inSelect: inSelect, flatten: flatten), named.Operator),
            _ => throw new DefensiveGuardException(true, "function argument type", arg.GetType())
        };
    }

    private FunctionArgumentClause NormalizeFunctionArgumentClause(FunctionArgumentClause clause, bool inSelect = false, bool flatten = false)
    {
        return clause switch
        {
            FunctionArgumentClause.OrderBy ob => new FunctionArgumentClause.OrderBy([.. ob.OrderByExpressions.Select(x => x with { Expression = NormalizeExpression(x.Expression, inSelect: inSelect, flatten: flatten) })]),
            FunctionArgumentClause.Limit li => new FunctionArgumentClause.Limit(NormalizeExpression(li.LimitExpression, inSelect: inSelect, flatten: flatten)),
            _ => clause
        };
    }

    private FunctionArguments NormalizeFunctionArguments(FunctionArguments args, bool inSelect = false, bool flatten = false)
    {
        switch (args)
        {
            case FunctionArguments.List li:
                SqlValueList<FunctionArgument>? newArgs = null;
                if (li.Arguments != null)
                {
                    newArgs = [.. li.Arguments.Select(x => NormalizeFunctionArgument(x, inSelect: inSelect, flatten: flatten))];
                }
                SqlValueList<FunctionArgumentClause>? clauses = null;
                if (li.Clauses != null)
                {
                    clauses = [.. li.Clauses.Select(x => NormalizeFunctionArgumentClause(x, inSelect: inSelect, flatten: flatten))];
                }
                return new FunctionArguments.List(newArgs, li.DuplicateTreatment, clauses);

                // TODO: Handle FunctionArguments.Subquery
        }
        return args;
    }

    protected virtual FunctionCall NormalizeFunctionCall(FunctionCall functionCall, bool inSelect = false, bool flatten = false)
    {
        FunctionArguments newArgs = NormalizeFunctionArguments(functionCall.Arguments, inSelect: inSelect, flatten: flatten);
        return functionCall with
        {
            Arguments = newArgs,
            Over = functionCall.Over is { } window ? window with
            {
                PartitionBy = window.PartitionBy == null ? null : [.. window.PartitionBy.Select(expression => NormalizeExpression(expression, inSelect, flatten))],
                OrderBy = NormalizeSelectOrderBy(window.OrderBy, inSelect, flatten)
            } : null
        };
    }

    protected virtual Exists NormalizeExists(Exists exists, bool flatten = false)
    {
        Select newSubquery = NormalizeSelectInternal(exists.Subquery, false, flatten: flatten);
        return new Exists(newSubquery, exists.Negated);
    }

    protected virtual InSubquery NormalizeInSubquery(InSubquery inSubquery)
    {
        // TODO: Implement full IN-subquery normalization.
        return inSubquery;
    }

    protected virtual Subquery NormalizeSubquery(Subquery subquery, bool flatten = false)
    {
        return subquery with { Query = NormalizeSelectInternal(subquery.Query, false, flatten) };
    }

    protected virtual Expression NormalizeIdentifier(CompoundIdentifier identifier, bool inSelect = false)
    {
        return inSelect ? NormalizeSelectableIdentifier(identifier) : ComponentNormalizer.NormalizeIdentifier(identifier);
    }

    protected virtual Expression NormalizeIdentifier(SingleIdentifier identifier, bool inSelect = false)
    {
        return inSelect ? NormalizeSelectableIdentifier(identifier) : ComponentNormalizer.NormalizeIdentifier(identifier);
    }

    protected virtual Expression NormalizeQualifiedWildcard(QualifiedWildcard wildcard)
    {
        return wildcard with { Qualifier = ComponentNormalizer.NormalizeObjectName(wildcard.Qualifier) };
    }

    protected virtual Expression NormalizeOtherExpression(Expression expression)
    {
        return expression;
    }

    private BinaryOperator NormalizeBinaryOperator(BinaryOperator binaryOperator, bool inSelect = false, bool flatten = false)
    {
        return new BinaryOperator(
            NormalizeExpression(binaryOperator.Left, inSelect: inSelect, flatten: flatten),
            binaryOperator.Operator,
            NormalizeExpression(binaryOperator.Right, inSelect: inSelect, flatten: flatten)
        );
    }

    private IsNull NormalizeIsNull(IsNull isNull, bool inSelect = false, bool flatten = false)
    {
        return new IsNull(NormalizeExpression(isNull.Expression, inSelect: inSelect, flatten: flatten), isNull.Negated);
    }

    protected virtual Expression NormalizeIsFalse(IsFalse isFalse, bool inSelect = false, bool flatten = false)
    {
        return isFalse with { Expression = NormalizeExpression(isFalse.Expression, inSelect: inSelect, flatten: flatten) };
    }

    protected virtual Expression NormalizeCast(Cast cast, bool inSelect = false, bool flatten = false)
    {
        return cast with { Expression = NormalizeExpression(cast.Expression, inSelect: inSelect, flatten: flatten) };
    }

    protected Expression NormalizeExpression(Expression expr, bool inSelect, bool flatten)
    {
        return expr switch
        {
            Between bet => new Between(
                NormalizeExpression(bet.Expression, inSelect: inSelect, flatten: flatten),
                NormalizeExpression(bet.Low, inSelect: inSelect, flatten: flatten),
                NormalizeExpression(bet.High, inSelect: inSelect, flatten: flatten), bet.Negated),
            BinaryOperator bo => NormalizeBinaryOperator(bo, inSelect: inSelect, flatten: flatten),
            Case ca => new Case(
                [.. ca.Conditions.Select(x => NormalizeExpression(x, inSelect: inSelect, flatten: flatten))],
                [.. ca.Results.Select(x => NormalizeExpression(x, inSelect: inSelect, flatten: flatten))]
            )
            {
                Operand = ca.Operand != null ? NormalizeExpression(ca.Operand, inSelect: inSelect, flatten: flatten) : null,
                ElseResult = ca.ElseResult != null ? NormalizeExpression(ca.ElseResult, inSelect: inSelect, flatten: flatten) : null
            },
            CompoundIdentifier ci => NormalizeIdentifier(ci, inSelect),
            QualifiedWildcard wildcard => NormalizeQualifiedWildcard(wildcard),
            Exists ex => NormalizeExists(ex, flatten: flatten),
            IsFalse isFalse => NormalizeIsFalse(isFalse, inSelect: inSelect, flatten: flatten),
            InList il => new InList(NormalizeExpression(il.Expression, inSelect: inSelect, flatten: flatten), [.. il.Items.Select(x => NormalizeExpression(x, inSelect: inSelect, flatten: flatten))], il.Negated),
            InSubquery isub => NormalizeInSubquery(isub),
            Subquery subquery => NormalizeSubquery(subquery, flatten),
            Nested nes when flatten => NormalizeExpression(nes.Expression, inSelect: inSelect, flatten: flatten),
            Nested nes => new Nested(NormalizeExpression(nes.Expression, inSelect: inSelect, flatten: flatten)),
            SingleIdentifier id => NormalizeIdentifier(id, inSelect),
            UnaryOperator uo => new UnaryOperator(NormalizeExpression(uo.Expression, inSelect: inSelect, flatten: flatten), uo.Operator),
            FunctionCall fun => NormalizeFunctionCall(fun, inSelect: inSelect, flatten: flatten),
            Cast cast => NormalizeCast(cast, inSelect: inSelect, flatten: flatten),
            Collate collate => collate with
            {
                Expression = NormalizeExpression(collate.Expression, inSelect, flatten),
                Collation = ComponentNormalizer.NormalizeObjectName(collate.Collation)
            },
            IsNull isNull => NormalizeIsNull(isNull, inSelect: inSelect, flatten: flatten),
            IsTrue check => check with { Expression = NormalizeExpression(check.Expression, inSelect, flatten) },
            IsUnknown check => check with { Expression = NormalizeExpression(check.Expression, inSelect, flatten) },
            IsDistinctFrom distinct => distinct with
            {
                Left = NormalizeExpression(distinct.Left, inSelect, flatten),
                Right = NormalizeExpression(distinct.Right, inSelect, flatten)
            },
            AtTimeZone zone => zone with
            {
                Timestamp = NormalizeExpression(zone.Timestamp, inSelect, flatten),
                Timezone = NormalizeExpression(zone.Timezone, inSelect, flatten)
            },
            Like like => like with
            {
                Expression = NormalizeExpression(like.Expression, inSelect, flatten),
                Pattern = NormalizeExpression(like.Pattern, inSelect, flatten)
            },
            ILike like => like with
            {
                Expression = NormalizeExpression(like.Expression, inSelect, flatten),
                Pattern = NormalizeExpression(like.Pattern, inSelect, flatten)
            },
            SimilarTo similar => similar with
            {
                Expression = NormalizeExpression(similar.Expression, inSelect, flatten),
                Pattern = NormalizeExpression(similar.Pattern, inSelect, flatten)
            },
            Regexp regexp => regexp with
            {
                Expression = NormalizeExpression(regexp.Expression, inSelect, flatten),
                Pattern = NormalizeExpression(regexp.Pattern, inSelect, flatten)
            },
            QuantifiedBinaryOperator quantified => quantified with
            {
                Left = NormalizeExpression(quantified.Left, inSelect, flatten),
                Right = NormalizeExpression(quantified.Right, inSelect, flatten)
            },
            Expressions.Tuple tuple => tuple with { Expressions = [.. tuple.Expressions.Select(expression => NormalizeExpression(expression, inSelect, flatten))] },
            Position position => position with
            {
                Expression = NormalizeExpression(position.Expression, inSelect, flatten),
                In = NormalizeExpression(position.In, inSelect, flatten)
            },
            Interval interval => interval with { Value = NormalizeExpression(interval.Value, inSelect, flatten) },
            LiteralValue or TypedString or PositionalParameter => expr,
            _ => NormalizeOtherExpression(expr)
        };
    }

    /// <inheritdoc />
    public Expression NormalizeExpression(Expression expr)
    {
        return NormalizeExpression(expr, inSelect: false, flatten: true);
    }

    /// <inheritdoc />
    public Expression NormalizeExpressionNoFlatten(Expression expr)
    {
        return NormalizeExpression(expr, inSelect: false, flatten: false);
    }

    private CompoundIdentifier NormalizeSelectableIdentifier(CompoundIdentifier identifier)
    {
        if (_pseudoTables == null)
        {
            return ComponentNormalizer.NormalizeIdentifier(identifier);
        }

        PseudoTableSet.SelectableItem realParts = _pseudoTables.GetRequiredSelectableElement(identifier, sourceRef: _sourceRef);
        return new CompoundIdentifier([.. realParts.ToArray().Select(x => new Identifier(x, _quoteStyle))]);
    }

    private CompoundIdentifier NormalizeSelectableIdentifier(SingleIdentifier identifier)
    {
        if (_pseudoTables == null)
        {
            return new CompoundIdentifier([ComponentNormalizer.NormalizeSingleIdentifier(identifier.Identifier)]);
        }

        PseudoTableSet.SelectableItem realParts = _pseudoTables.GetRequiredSelectableElement(identifier.Identifier.Name, sourceRef: _sourceRef);
        return new CompoundIdentifier([.. realParts.ToArray().Select(x => new Identifier(x, _quoteStyle))]);
    }

    private SimpleSelectItem NormalizeSimpleSelectItem(SimpleSelectItem item, bool returnsValues, bool flatten = false)
    {
        SourceRef? itemSourceRef = null;
        if (_sourceRef != null && item.Meta is { Start: not null, End: not null })
        {
            itemSourceRef = new SourceRef(_sourceRef.Value.SourceId, item.Meta.Start.Value, item.Meta.End.Value);
        }

        SimpleSelectItem result = item switch
        {
            SimpleSelectItem.ExpressionWithAlias expr => new SimpleSelectItem.ExpressionWithAlias(NormalizeExpression(expr.Expression, inSelect: true, flatten: flatten), ComponentNormalizer.NormalizeSingleIdentifier(expr.Alias)),
            SimpleSelectItem.UnnamedExpression expr => expr.Expression switch
            {
                SingleIdentifier ident => new SimpleSelectItem.ExpressionWithAlias(NormalizeSelectableIdentifier(ident), ComponentNormalizer.NormalizeSingleIdentifier(ident.Identifier)),
                CompoundIdentifier ident => new SimpleSelectItem.ExpressionWithAlias(NormalizeSelectableIdentifier(ident), ComponentNormalizer.NormalizeSingleIdentifier(ident.Identifiers.Last())),
                Wildcard => throw new DefinitionException.ViewSelectWildcard(_name, itemSourceRef),
                QualifiedWildcard => throw new DefinitionException.ViewSelectWildcard(_name, itemSourceRef),
                _ when !returnsValues => item,
                _ => throw new DefinitionException.ViewSelectWithoutAlias(_name, itemSourceRef)
            },
            SimpleSelectItem.QualifiedWildcard => throw new DefinitionException.ViewSelectWildcard(_name, itemSourceRef),
            SimpleSelectItem.Wildcard => throw new DefinitionException.ViewSelectWildcard(_name, itemSourceRef),
            _ => throw new NotImplementedException()
        } ?? throw new DefensiveGuardException(true, "select item type", item.GetType());

        return result;
    }

    private TableFactor.Table NormalizeTableFactor(TableFactor.Table table)
    {
        Identifier? newAlias = ComponentNormalizer.NormalizeSingleIdentifierNullable(table.Alias);

        ObjectName objectName = table.Name;
        if (_pseudoTables != null)
        {
            PseudoTable tableSource = _pseudoTables.GetRequiredPseudoTable(table.Name, _sourceRef);
            string refName = table.Alias?.Name ?? tableSource.Name;
            if (!string.IsNullOrEmpty(tableSource.Identifier?.Schema.Name))
            {
                objectName = new ObjectName([tableSource.Identifier.Schema.ToSimpleIdentifier(_quoteStyle), new Identifier(tableSource.Name, _quoteStyle)]);
            }
            _pseudoTables.AddLocalSource(refName, tableSource, newAlias?.Name);
        }

        ObjectName newName = ComponentNormalizer.NormalizeObjectName(objectName);
        return new TableFactor.Table(newName)
        {
            Alias = newAlias
        };
    }

    private TableFactor.Derived NormalizeTableFactor(TableFactor.Derived table)
    {
        Identifier newAlias = ComponentNormalizer.NormalizeSingleIdentifier(table.Alias!);
        Select newSubQuery = NormalizeSelectInternal(table.SubQuery, true);

        var newTableFactor = new TableFactor.Derived(newSubQuery)
        {
            Alias = newAlias
        };

        var newSource = newSubQuery.ToPseudoTable(newAlias.Name, null, _sourceRef, PseudoTableType.DerivedTable);
        _pseudoTables?.AddExternalSource(newAlias.Name, newSource);
        _pseudoTables?.AddLocalSource(newAlias.Name, newSource, newAlias.Name);

        return newTableFactor;
    }

    private TableFactor.NestedJoin NormalizeTableFactor(TableFactor.NestedJoin table)
    {
        Identifier? newAlias = ComponentNormalizer.NormalizeSingleIdentifierNullable(table.Alias);
        return new TableFactor.NestedJoin()
        {
            TableWithJoins = NormalizeTableWithJoins(table.TableWithJoins),
            Alias = newAlias
        };
    }

    private TableFactor NormalizeTableFactor(TableFactor table)
    {
        return table switch
        {
            TableFactor.Table t => NormalizeTableFactor(t),
            TableFactor.Derived d => NormalizeTableFactor(d),
            TableFactor.NestedJoin n => NormalizeTableFactor(n),
            _ => throw new DefensiveGuardException(true, "table factor type", table.GetType())
        };
    }

    private JoinConstraint NormalizeJoinConstraint(JoinConstraint constraint, bool flatten = false)
    {
        if (constraint is JoinConstraint.On joinOn)
        {
            return new JoinConstraint.On(NormalizeExpression(joinOn.Expression, inSelect: true, flatten: flatten));
        }
        else if (constraint is JoinConstraint.Using)
        {
            // TODO: Implement JOIN...USING normalization
            throw new NotImplementedException("JOIN...USING is not (yet) supported.");
        }
        return constraint;
    }

    private Join NormalizeJoin(Join join, bool flatten = false)
    {
        TableFactor? relation = join.Relation != null ? NormalizeTableFactor(join.Relation) : null;
        JoinOperator? op = join.JoinOperator switch
        {
            JoinOperator.Inner ji => new JoinOperator.Inner(NormalizeJoinConstraint(ji.JoinConstraint, flatten: flatten)),
            JoinOperator.LeftOuter jo => new JoinOperator.LeftOuter(NormalizeJoinConstraint(jo.JoinConstraint, flatten: flatten)),
            JoinOperator.RightOuter jo => new JoinOperator.RightOuter(NormalizeJoinConstraint(jo.JoinConstraint, flatten: flatten)),
            JoinOperator.FullOuter jo => new JoinOperator.FullOuter(NormalizeJoinConstraint(jo.JoinConstraint, flatten: flatten)),
            _ => join.JoinOperator
        };

        return new Join(relation, op);
    }

    private static TableFactor FlattenNestedJoin(TableFactor.NestedJoin nestedJoin, List<Join> allJoins)
    {
        TableWithJoins inner = nestedJoin.TableWithJoins;
        List<Join> innerJoins = inner.Joins != null ? [.. inner.Joins] : [];
        innerJoins.AddRange(allJoins);
        allJoins.Clear();
        allJoins.AddRange(innerJoins);

        if (inner.Relation is TableFactor.NestedJoin { Alias: null } nestedInner)
        {
            return FlattenNestedJoin(nestedInner, allJoins);
        }
        return inner.Relation;
    }

    private TableWithJoins NormalizeTableWithJoins(TableWithJoins tableWithJoins, bool flatten = false)
    {
        TableFactor relation = tableWithJoins.Relation;
        List<Join> allJoins = tableWithJoins.Joins != null ? [.. tableWithJoins.Joins] : [];

        if (relation is TableFactor.NestedJoin { Alias: null } nestedJoin)
        {
            relation = FlattenNestedJoin(nestedJoin, allJoins);
        }

        TableFactor normalizedRelation = NormalizeTableFactor(relation);
        SqlValueList<Join>? normalizedJoins = allJoins.Count > 0 ? [.. allJoins.Select(join => NormalizeJoin(join, flatten: flatten))] : null;

        return new TableWithJoins(normalizedRelation)
        {
            Joins = normalizedJoins
        };
    }

    private SqlValueList<TableWithJoins>? NormalizeFrom(SqlValueList<TableWithJoins>? from, bool flatten = false)
    {
        if (from == null)
        {
            return null;
        }

        return [.. from.Select(tableWithJoins => NormalizeTableWithJoins(tableWithJoins, flatten: flatten))];
    }

    private SimpleSelect NormalizeSimpleSelect(SimpleSelect select, bool returnsValues, bool flatten = false)
    {
        SqlValueList<TableWithJoins>? from = NormalizeFrom(select.From, flatten: flatten);
        Expression? where = select.Where != null ? NormalizeExpression(select.Where, inSelect: true, flatten: flatten) : null;
        SqlValueList<Expression>? groupBys = select.GroupBy?.Select(x => NormalizeExpression(x, inSelect: true, flatten: flatten)).ToSequence();
        Expression? having = select.Having != null ? NormalizeExpression(select.Having, inSelect: true, flatten: flatten) : null;

        SqlValueList<SimpleSelectItem> selections = select.Selections.Select(x => NormalizeSimpleSelectItem(x, returnsValues, flatten: flatten)).ToSequence();

        return new SimpleSelect(selections)
        {
            Distinct = select.Distinct,
            From = from,
            Where = where,
            GroupBy = groupBys,
            Having = having
        };
    }

    private SimpleSelect NormalizeSimpleSelectWithPseudoTableIsolation(SimpleSelect select, bool returnsValues, bool flatten = false)
    {
        PseudoTableSet? pseudoTables = _pseudoTables;
        _pseudoTables = _pseudoTables?.Clone();

        select = NormalizeSimpleSelect(select, returnsValues, flatten: flatten);

        _pseudoTables = pseudoTables;

        return select;
    }

    private SelectBody NormalizeSelectBody(SelectBody body, bool pseudoTablesAlreadyIsolated = false, bool returnsValues = false, bool flatten = false)
    {
        switch (body)
        {
            case SelectBody.SimpleSelectQuery ssq:
            {
                SimpleSelect newSelect = pseudoTablesAlreadyIsolated
                    ? NormalizeSimpleSelect(ssq.Query, returnsValues, flatten: flatten)
                    : NormalizeSimpleSelectWithPseudoTableIsolation(ssq.Query, returnsValues, flatten: flatten);
                return new SelectBody.SimpleSelectQuery(newSelect);
            }
            case SelectBody.Nested nested:
            {
                Select normalizedQuery = NormalizeSelect(nested.Query, topLevel: false, returnsValues, flatten: flatten);
                bool hasQueryClauses = normalizedQuery.CommonTableExpression != null
                    || normalizedQuery.OrderBy != null
                    || normalizedQuery.Limit != null
                    || normalizedQuery.SelectInto != null;
                return flatten && !hasQueryClauses
                    ? normalizedQuery.Body
                    : new SelectBody.Nested(normalizedQuery);
            }
            case SelectBody.SetOperation so:
            {
                SelectBody left = NormalizeSelectBody(so.Left, false, returnsValues, flatten: flatten);
                SelectBody right = NormalizeSelectBody(so.Right, false, returnsValues, flatten: flatten);
                return new SelectBody.SetOperation(left, so.Operator, right, so.Quantifier);
            }
            case SelectBody.ValuesQuery:
                throw new NotImplementedException("VALUES in views is not (yet) supported.");
            default:
                throw new DefensiveGuardException(true, "select body type", body.GetType());
        }
    }

    private SqlValueList<OrderBy>? NormalizeSelectOrderBy(SqlValueList<OrderBy>? orderBy, bool inSelect, bool flatten = false)
    {
        if (orderBy == null)
        {
            return null;
        }

        return orderBy.Select(ob => ob with { Expression = NormalizeExpression(ob.Expression, inSelect: inSelect, flatten: flatten) }).ToSequence();
    }

    private (CommonTableExpressionBody, PseudoTableSet?) NormalizeCteBody(CommonTableExpressionBody cteBody, PseudoTableSet? pseudoTables)
    {
        INormalizeSql normalizer = ConstructNewNormalizer();
        // To account for recursive references, we need to add the CTE tables to the context before normalizing the body.
        // We'll pass a clone so that outside this function, the normalized body can be used to create the pseudo-table.
        PseudoTableSet? currPseudoTables = pseudoTables?.CloneExternalOnly();
        if (currPseudoTables != null)
        {
            cteBody.AddTablesToContext(currPseudoTables);
        }
        Select newSelect = normalizer.NormalizeSelect(cteBody.Query, _activeSchema, currPseudoTables, _name);
        SqlValueList<Identifier>? newColumns = null;
        if (cteBody.Columns.SafeAny())
        {
            newColumns = [.. cteBody.Columns!.Select(ComponentNormalizer.NormalizeSingleIdentifier)];
        }

        var newBody = new CommonTableExpressionBody(ComponentNormalizer.NormalizeSingleIdentifier(cteBody.Name, _normalization.CteDeclarationNameQuotationHandling), newColumns, newSelect, cteBody.From != null ? ComponentNormalizer.NormalizeSingleIdentifier(cteBody.From) : null);
        return (newBody, pseudoTables);
    }

    private CommonTableExpression NormalizeCte(CommonTableExpression cte)
    {
        PseudoTableSet? pseudoTables = _pseudoTables?.CloneExternalOnly();
        var newTables = new SqlValueList<CommonTableExpressionBody>();

        foreach (CommonTableExpressionBody table in cte.Tables)
        {
            (CommonTableExpressionBody newBody, PseudoTableSet? newPseudoTables) = NormalizeCteBody(table, pseudoTables);
            pseudoTables = newPseudoTables;
            newTables.Add(newBody);
            PseudoTable newPseudoTable = newBody.Columns != null
                ? new PseudoTable(newBody.Name.Name, null, [.. newBody.Columns.Select(x => x.Name)], PseudoTableType.CommonTableExpression)
                : newBody.Query.ToPseudoTable(newBody.Name.Name, null, _sourceRef, PseudoTableType.CommonTableExpression);
            _pseudoTables?.AddExternalSource(newBody.Name.Name, newPseudoTable);
        }

        return cte with { Tables = newTables };
    }

    private Limit NormalizeLimit(Limit limit, bool flatten = false)
    {
        return limit switch
        {
            Limit.ExpressionLimit el => new Limit.ExpressionLimit(NormalizeExpression(el.Expression, inSelect: true, flatten: flatten)),
            Limit.MyCommaSeparated cs => new Limit.MyCommaSeparated(NormalizeExpression(cs.LimitExpression, inSelect: true, flatten: flatten), cs.OffsetExpression != null ? NormalizeExpression(cs.OffsetExpression, inSelect: true, flatten: flatten) : null),
            Limit.MyLimitOffset lo => new Limit.MyLimitOffset(NormalizeExpression(lo.LimitExpression, inSelect: true, flatten: flatten), lo.OffsetExpression != null ? NormalizeExpression(lo.OffsetExpression, inSelect: true, flatten: flatten) : null),
            _ => throw new NotImplementedException()
        };
    }

    private Select NormalizeSelect(Select select, bool topLevel, bool returnsValues, bool flatten = false)
    {
        PseudoTableSet? outerPseudoTables = _pseudoTables;
        if (!topLevel)
        {
            _pseudoTables = outerPseudoTables?.Clone();
        }

        _pseudoTables?.EnterSelectScope();

        CommonTableExpression? cte = select.CommonTableExpression != null ? NormalizeCte(select.CommonTableExpression) : null;
        SelectBody body = NormalizeSelectBody(select.Body, pseudoTablesAlreadyIsolated: true, returnsValues, flatten: flatten);
        // ORDER BY over a set operation or parenthesized query names output columns, not source tables.
        bool orderByUsesSources = select.Body is SelectBody.SimpleSelectQuery;
        SqlValueList<OrderBy>? orderBy = NormalizeSelectOrderBy(select.OrderBy, orderByUsesSources, flatten: flatten);
        Limit? limit = select.Limit != null ? NormalizeLimit(select.Limit, flatten: flatten) : null;

        if (topLevel)
        {
            _pseudoTables?.LeaveSelectScope();
        }
        else
        {
            _pseudoTables = outerPseudoTables;
        }

        return new Select(body)
        {
            CommonTableExpression = cte,
            OrderBy = orderBy,
            Limit = limit,
        };
    }

    private Select NormalizeSelectInternal(Select select, bool returnsValues, bool flatten = false)
    {
        select = NormalizeSelect(select, topLevel: false, returnsValues: returnsValues, flatten: flatten);
        return select;
    }

    /// <inheritdoc />
    public Select NormalizeSelect(Select selectStatement, SchemaIdentifier? activeSchema = null, PseudoTableSet? pseudoTables = null, ObjectIdentifier? name = null, SourceRef? sourceRef = null)
    {
        _name = name;
        _activeSchema = activeSchema;
        _pseudoTables = pseudoTables;
        _sourceRef = sourceRef;

        Select newSelect = NormalizeSelect(selectStatement, topLevel: true, returnsValues: true, flatten: true);

        _pseudoTables = null;
        _sourceRef = null;
        _activeSchema = null;
        _name = null;

        return newSelect;
    }
}
