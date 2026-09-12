using System.Diagnostics;
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

    private FunctionArgumentExpression NormalizeFunctionArgumentExpression(FunctionArgumentExpression expr, bool inSelect = false, bool flatten = false)
    {
        return expr switch
        {
            FunctionArgumentExpression.Wildcard => expr,
            FunctionArgumentExpression.QualifiedWildcard qw => new FunctionArgumentExpression.QualifiedWildcard(ComponentNormalizer.NormalizeObjectName(qw.Name)),
            FunctionArgumentExpression.FunctionExpression fe => new FunctionArgumentExpression.FunctionExpression(NormalizeExpression(fe.Expression, inSelect: inSelect, flatten: flatten)),
            _ => throw new UnreachableException()
        };
    }

    private FunctionArgument NormalizeFunctionArgument(FunctionArgument arg, bool inSelect = false, bool flatten = false)
    {
        return arg switch
        {
            FunctionArgument.Unnamed un => new FunctionArgument.Unnamed(NormalizeFunctionArgumentExpression(un.Argument, inSelect: inSelect, flatten: flatten)),
            FunctionArgument.Named named => new FunctionArgument.Named(ComponentNormalizer.NormalizeSingleIdentifier(named.Name), NormalizeFunctionArgumentExpression(named.Argument, inSelect: inSelect, flatten: flatten), named.Operator),
            _ => throw new UnreachableException()
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

    private static ObjectName NormalizeFunctionName(ObjectName name)
    {
        if (name.Values.Count == 1 && name.Values[0].Name.Equals("NOW", StringComparison.OrdinalIgnoreCase))
        {
            return new ObjectName([new Identifier("CURRENT_TIMESTAMP")]);
        }
        return name;
    }

    private FunctionCall NormalizeFunctionCall(FunctionCall functionCall, bool inSelect = false, bool flatten = false)
    {
        ObjectName funcName = NormalizeFunctionName(functionCall.Name);
        FunctionArguments newArgs = NormalizeFunctionArguments(functionCall.Arguments, inSelect: inSelect, flatten: flatten);
        return new FunctionCall(funcName)
        {
            Arguments = newArgs,
        };
    }

    private Exists NormalizeExists(Exists exists, bool flatten = false)
    {
        Select subquery = exists.Subquery;
        if (subquery.Limit != null)
        {
            subquery = subquery with { Limit = null };
        }

        Select newSubquery = NormalizeSelectInternal(subquery, false, flatten: flatten);
        return new Exists(newSubquery, exists.Negated);
    }

    private static InSubquery NormalizeInSubquery(InSubquery inSubquery)
    {
        // TODO: Implement full IN-subquery normalization.
        return inSubquery;
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

    protected virtual Expression NormalizeCast(Cast cast, bool inSelect = false, bool flatten = false)
    {
        return new Cast(NormalizeExpression(cast.Expression, inSelect: inSelect, flatten: flatten), cast.DataType);
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
            CompoundIdentifier ci when inSelect => NormalizeSelectableIdentifier(ci),
            CompoundIdentifier ci => ComponentNormalizer.NormalizeIdentifier(ci),
            Exists ex => NormalizeExists(ex, flatten: flatten),
            // EXISTS(...) IS FALSE is how MySQL/MariaDB stores NOT EXISTS(...) in INFORMATION_SCHEMA.
            // Canonicalize to Exists(negated: true) so both sides compare equal.
            IsFalse { Negated: false, Expression: Exists { Negated: false } innerExists } => NormalizeExists(innerExists with { Negated = true }, flatten: flatten),
            IsFalse isFalse => new IsFalse(NormalizeExpression(isFalse.Expression, inSelect: inSelect, flatten: flatten), isFalse.Negated),
            InList il => new InList(NormalizeExpression(il.Expression, inSelect: inSelect, flatten: flatten), [.. il.Items.Select(x => NormalizeExpression(x, inSelect: inSelect, flatten: flatten))], il.Negated),
            InSubquery isub => NormalizeInSubquery(isub),
            Nested nes when flatten => NormalizeExpression(nes.Expression, inSelect: inSelect, flatten: flatten),
            Nested nes => new Nested(NormalizeExpression(nes.Expression, inSelect: inSelect, flatten: flatten)),
            SingleIdentifier id when inSelect => NormalizeSelectableIdentifier(id),
            SingleIdentifier id => ComponentNormalizer.NormalizeIdentifier(id),
            UnaryOperator uo => new UnaryOperator(NormalizeExpression(uo.Expression, inSelect: inSelect, flatten: flatten), uo.Operator),
            FunctionCall fun => NormalizeFunctionCall(fun, inSelect: inSelect, flatten: flatten),
            Cast cast => NormalizeCast(cast, inSelect: inSelect, flatten: flatten),
            IsNull isNull => NormalizeIsNull(isNull, inSelect: inSelect, flatten: flatten),
            _ => expr
        };
    }

    public Expression NormalizeExpression(Expression expr)
    {
        return NormalizeExpression(expr, inSelect: false, flatten: true);
    }

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
        } ?? throw new UnreachableException();

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
            _ => throw new UnreachableException()
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

    private SimpleSelect NormalizeSimpleSelectInternal(SimpleSelect select, bool returnsValues, bool flatten = false)
    {
        PseudoTableSet? pseudoTables = _pseudoTables;
        _pseudoTables = _pseudoTables?.Clone();

        select = NormalizeSimpleSelect(select, returnsValues, flatten: flatten);

        _pseudoTables = pseudoTables;

        return select;
    }

    private SelectBody NormalizeSelectBody(SelectBody body, bool topLevel = false, bool returnsValues = false, bool flatten = false)
    {
        if (body is SelectBody.SimpleSelectQuery ssq)
        {
            SimpleSelect newSelect = topLevel
                ? NormalizeSimpleSelect(ssq.Query, returnsValues, flatten: flatten)
                : NormalizeSimpleSelectInternal(ssq.Query, returnsValues, flatten: flatten);
            return new SelectBody.SimpleSelectQuery(newSelect);
        }
        else if (body is SelectBody.SelectQuery sq)
        {
            SelectBody newSelect = NormalizeSelectBody(sq.Query.Body, false, returnsValues, flatten: flatten);
            return new SelectBody.SelectQuery(new Select(newSelect)
            {
                OrderBy = sq.Query.OrderBy?.Select(x => x with { Expression = NormalizeExpression(x.Expression, inSelect: true, flatten: flatten) }).ToSequence(),
                Limit = sq.Query.Limit != null ? NormalizeLimit(sq.Query.Limit, flatten: flatten) : null,
            });
        }
        else if (body is SelectBody.SetOperation so)
        {
            SelectBody left = NormalizeSelectBody(so.Left, false, returnsValues, flatten: flatten);
            SelectBody right = NormalizeSelectBody(so.Right, false, returnsValues, flatten: flatten);
            return new SelectBody.SetOperation(left, so.Operator, right, so.Quantifier);
        }
        else if (body is SelectBody.ValuesQuery)
        {
            throw new NotImplementedException("VALUES in views is not (yet) supported.");
        }
        throw new UnreachableException();
    }

    private SqlValueList<OrderBy>? NormalizeSelectOrderBy(SqlValueList<OrderBy>? orderBy, bool flatten = false)
    {
        if (orderBy == null)
        {
            return null;
        }

        return orderBy.Select(ob => ob with { Expression = NormalizeExpression(ob.Expression, inSelect: true, flatten: flatten) }).ToSequence();
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
        _pseudoTables?.EnterSelectScope();

        CommonTableExpression? cte = select.CommonTableExpression != null ? NormalizeCte(select.CommonTableExpression) : null;
        SelectBody body = NormalizeSelectBody(select.Body, topLevel, returnsValues, flatten: flatten);
        SqlValueList<OrderBy>? orderBy = NormalizeSelectOrderBy(select.OrderBy, flatten: flatten);
        Limit? limit = select.Limit != null ? NormalizeLimit(select.Limit, flatten: flatten) : null;

        _pseudoTables?.LeaveSelectScope();

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
