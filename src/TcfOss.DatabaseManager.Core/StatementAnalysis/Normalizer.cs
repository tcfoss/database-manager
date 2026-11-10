using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Expressions.Components;
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
            WithinGroup = NormalizeSelectOrderBy(functionCall.WithinGroup, inSelect, flatten),
            Filter = NormalizeNullableExpression(functionCall.Filter, inSelect, flatten),
            Over = functionCall.Over == null ? null : NormalizeWindowSpec(functionCall.Over, inSelect, flatten)
        };
    }

    private WindowSpec NormalizeWindowSpec(WindowSpec window, bool inSelect, bool flatten)
    {
        return window switch
        {
            WindowSpec.NamedReference named => new WindowSpec.NamedReference(ComponentNormalizer.NormalizeSingleIdentifier(named.WindowName)),
            WindowSpec.InlineSpecification inline => NormalizeWindowSpec(inline, inSelect, flatten),
            _ => throw new DefensiveGuardException(true, "normalize window spec", window.GetType())
        };
    }

    private WindowSpec.InlineSpecification NormalizeWindowSpec(WindowSpec.InlineSpecification inline, bool inSelect, bool flatten)
    {
        return new WindowSpec.InlineSpecification
        {
            BaseWindow = inline.BaseWindow == null ? null : ComponentNormalizer.NormalizeSingleIdentifierNullable(inline.BaseWindow),
            PartitionBy = inline.PartitionBy == null ? null : [.. inline.PartitionBy.Select(expression => NormalizeExpression(expression, inSelect, flatten))],
            OrderBy = NormalizeSelectOrderBy(inline.OrderBy, inSelect, flatten),
            Frame = inline.Frame == null ? null : inline.Frame with
            {
                Start = NormalizeWindowFrameBound(inline.Frame.Start, inSelect, flatten),
                End = inline.Frame.End == null ? null : NormalizeWindowFrameBound(inline.Frame.End, inSelect, flatten)
            }
        };
    }

    private WindowFrameBound NormalizeWindowFrameBound(WindowFrameBound bound, bool inSelect, bool flatten)
    {
        return bound switch
        {
            WindowFrameBound.Preceding preceding => preceding with { Offset = NormalizeExpression(preceding.Offset, inSelect, flatten) },
            WindowFrameBound.Following following => following with { Offset = NormalizeExpression(following.Offset, inSelect, flatten) },
            _ => bound
        };
    }

    protected virtual Exists NormalizeExists(Exists exists, bool flatten = false)
    {
        Select newSubquery = NormalizeSelectInternal(exists.Subquery, false, flatten: flatten);
        return new Exists(newSubquery, exists.Negated);
    }

    protected virtual InSubquery NormalizeInSubquery(InSubquery inSubquery, bool inSelect = false, bool flatten = false)
    {
        Expression? expression = NormalizeNullableExpression(inSubquery.Expression, inSelect, flatten);
        var query = new Select(new SelectBody.SimpleSelectQuery(inSubquery.Subquery));
        Select normalizedQuery = NormalizeSelectInternal(query, returnsValues: false, flatten: flatten);
        return inSubquery with
        {
            Expression = expression,
            Subquery = ((SelectBody.SimpleSelectQuery)normalizedQuery.Body).Query
        };
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

    private PatternEscape? NormalizePatternEscape(PatternEscape? escape, bool inSelect, bool flatten)
    {
        return escape is PatternEscape.Expression expression
            ? new PatternEscape.Expression(NormalizeExpression(expression.Value, inSelect, flatten))
            : escape;
    }

    protected Expression NormalizeExpression(Expression expr, bool inSelect, bool flatten)
    {
        return expr switch
        {
            Between bet => bet with
            {
                Expression = NormalizeExpression(bet.Expression, inSelect, flatten),
                Low = NormalizeExpression(bet.Low, inSelect, flatten),
                High = NormalizeExpression(bet.High, inSelect, flatten)
            },
            BinaryOperator bo => NormalizeBinaryOperator(bo, inSelect: inSelect, flatten: flatten),
            PgBinaryOperator op => op with
            {
                Left = NormalizeExpression(op.Left, inSelect, flatten),
                Right = NormalizeExpression(op.Right, inSelect, flatten),
                Operator = op.Operator with { Schema = op.Operator.Schema == null ? null : ComponentNormalizer.NormalizeSingleIdentifier(op.Operator.Schema) }
            },
            PgUnaryOperator op => op with
            {
                Expression = NormalizeExpression(op.Expression, inSelect, flatten),
                Operator = op.Operator with { Schema = op.Operator.Schema == null ? null : ComponentNormalizer.NormalizeSingleIdentifier(op.Operator.Schema) }
            },
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
            ArrayConstructor.Elements array => array with
            {
                Values = [.. array.Values.Select(value => NormalizeExpression(value, inSelect, flatten))],
                UseArrayKeyword = flatten || array.UseArrayKeyword
            },
            ArrayConstructor.Subquery array => array with { Query = NormalizeSelectInternal(array.Query, false, flatten) },
            ArraySubscript subscript => subscript with
            {
                Array = NormalizeExpression(subscript.Array, inSelect, flatten),
                Dimensions = [.. subscript.Dimensions.Select(dimension => dimension switch
                {
                    ArraySubscriptDimension.Index index => new ArraySubscriptDimension.Index(NormalizeExpression(index.Expression, inSelect, flatten)),
                    ArraySubscriptDimension.Slice slice => new ArraySubscriptDimension.Slice(NormalizeNullableExpression(slice.LowerBound, inSelect, flatten), NormalizeNullableExpression(slice.UpperBound, inSelect, flatten)),
                    _ => dimension
                })]
            },
            CompositeAccess.Field field => field with
            {
                Expression = NormalizeExpression(field.Expression, inSelect, flatten),
                Name = ComponentNormalizer.NormalizeSingleIdentifier(field.Name)
            },
            CompositeAccess.Wildcard fields => fields with { Expression = NormalizeExpression(fields.Expression, inSelect, flatten) },
            IsFalse isFalse => NormalizeIsFalse(isFalse, inSelect: inSelect, flatten: flatten),
            InList il => new InList(NormalizeExpression(il.Expression, inSelect: inSelect, flatten: flatten), [.. il.Items.Select(x => NormalizeExpression(x, inSelect: inSelect, flatten: flatten))], il.Negated),
            InSubquery isub => NormalizeInSubquery(isub, inSelect, flatten),
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
            AtLocal local => local with { Timestamp = NormalizeExpression(local.Timestamp, inSelect, flatten) },
            Like like => like with
            {
                Expression = NormalizeExpression(like.Expression, inSelect, flatten),
                Pattern = NormalizeExpression(like.Pattern, inSelect, flatten),
                Escape = NormalizePatternEscape(like.Escape, inSelect, flatten)
            },
            ILike like => like with
            {
                Expression = NormalizeExpression(like.Expression, inSelect, flatten),
                Pattern = NormalizeExpression(like.Pattern, inSelect, flatten),
                Escape = NormalizePatternEscape(like.Escape, inSelect, flatten)
            },
            SimilarTo similar => similar with
            {
                Expression = NormalizeExpression(similar.Expression, inSelect, flatten),
                Pattern = NormalizeExpression(similar.Pattern, inSelect, flatten),
                Escape = NormalizePatternEscape(similar.Escape, inSelect, flatten)
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
            RowConstructor row => row with { Fields = [.. row.Fields.Select(field => NormalizeExpression(field, inSelect, flatten))] },
            Position position => position with
            {
                Expression = NormalizeExpression(position.Expression, inSelect, flatten),
                In = NormalizeExpression(position.In, inSelect, flatten)
            },
            Interval interval => interval with { Value = NormalizeExpression(interval.Value, inSelect, flatten) },
            LiteralValue or TypedString or PositionalParameter or PgIntervalLiteral => expr,
            _ => NormalizeOtherExpression(expr)
        };
    }

    private Expression? NormalizeNullableExpression(Expression? expr, bool inSelect, bool flatten)
    {
        return expr is null ? null : NormalizeExpression(expr, inSelect, flatten);
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
                _ when !returnsValues => (SimpleSelectItem)(expr with { Expression = NormalizeExpression(expr.Expression, inSelect: true, flatten: flatten) }),
                _ => throw new DefinitionException.ViewSelectWithoutAlias(_name, itemSourceRef)
            },
            SimpleSelectItem.QualifiedWildcard => throw new DefinitionException.ViewSelectWildcard(_name, itemSourceRef),
            SimpleSelectItem.Wildcard => throw new DefinitionException.ViewSelectWildcard(_name, itemSourceRef),
            _ => throw new NotImplementedException()
        } ?? throw new DefensiveGuardException(true, "select item type", item.GetType());

        return result;
    }


    private SqlValueList<Identifier>? NormalizeAliasColumns(SqlValueList<Identifier>? columns)
    {
        return columns == null ? null : [.. columns.Select(ComponentNormalizer.NormalizeSingleIdentifier)];
    }

    private TableFunctionCall NormalizeTableFunctionCall(TableFunctionCall function, bool flatten)
    {
        return function with
        {
            Call = NormalizeFunctionCall(function.Call, inSelect: true, flatten: flatten),
            Columns = function.Columns == null ? null : [.. function.Columns.Select(column => column with
            {
                Name = ComponentNormalizer.NormalizeSingleIdentifier(column.Name),
                Collation = column.Collation == null ? null : ComponentNormalizer.NormalizeObjectName(column.Collation)
            })]
        };
    }

    private void AddNormalizedFunctionSource(TableFactor table, string defaultName, Identifier? normalizedAlias)
    {
        string sourceName = table.Alias?.Name ?? defaultName;
        PseudoTable source = new PseudoTable.Unbound(sourceName, null, PseudoTableType.Table).RenameColumns(table.AliasColumns);
        _pseudoTables?.AddLocalSource(sourceName, source, normalizedAlias?.Name);
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
            _pseudoTables.AddLocalSource(refName, tableSource.RenameColumns(table.AliasColumns), newAlias?.Name);
        }

        ObjectName newName = ComponentNormalizer.NormalizeObjectName(objectName);
        return new TableFactor.Table(newName)
        {
            Alias = newAlias,
            AliasColumns = NormalizeAliasColumns(table.AliasColumns),
            IsLateral = table.IsLateral
        };
    }

    private TableFactor.Derived NormalizeTableFactor(TableFactor.Derived table, bool flatten)
    {
        Identifier newAlias = ComponentNormalizer.NormalizeSingleIdentifier(table.Alias!);
        Select newSubQuery = NormalizeSelectInternal(table.SubQuery, true, flatten);

        var newTableFactor = new TableFactor.Derived(newSubQuery)
        {
            Alias = newAlias,
            AliasColumns = NormalizeAliasColumns(table.AliasColumns),
            IsLateral = table.IsLateral
        };

        PseudoTable newSource = newSubQuery
            .ToPseudoTable(newAlias.Name, null, _sourceRef, PseudoTableType.DerivedTable)
            .RenameColumns(table.AliasColumns);
        _pseudoTables?.AddExternalSource(newAlias.Name, newSource);
        _pseudoTables?.AddLocalSource(newAlias.Name, newSource, newAlias.Name);

        return newTableFactor;
    }

    private TableFactor.NestedJoin NormalizeTableFactor(TableFactor.NestedJoin table, bool flatten)
    {
        Identifier? newAlias = ComponentNormalizer.NormalizeSingleIdentifierNullable(table.Alias);
        return new TableFactor.NestedJoin()
        {
            TableWithJoins = NormalizeTableWithJoins(table.TableWithJoins, flatten),
            Alias = newAlias,
            AliasColumns = NormalizeAliasColumns(table.AliasColumns),
            IsLateral = table.IsLateral
        };
    }

    private TableFactor.TableFunction NormalizeTableFactor(TableFactor.TableFunction table, bool flatten)
    {
        TableFactor.TableFunction normalized = table with
        {
            Function = NormalizeTableFunctionCall(table.Function, flatten),
            Alias = ComponentNormalizer.NormalizeSingleIdentifierNullable(table.Alias),
            AliasColumns = NormalizeAliasColumns(table.AliasColumns)
        };
        AddNormalizedFunctionSource(table, table.Function.Call.Name.Values.Last().Name, normalized.Alias);
        return normalized;
    }

    private TableFactor.RowsFrom NormalizeTableFactor(TableFactor.RowsFrom table, bool flatten)
    {
        TableFactor.RowsFrom normalized = table with
        {
            Functions = [.. table.Functions.Select(function => NormalizeTableFunctionCall(function, flatten))],
            Alias = ComponentNormalizer.NormalizeSingleIdentifierNullable(table.Alias),
            AliasColumns = NormalizeAliasColumns(table.AliasColumns)
        };
        AddNormalizedFunctionSource(table, table.Functions[0].Call.Name.Values.Last().Name, normalized.Alias);
        return normalized;
    }

    private TableFactor NormalizeTableFactor(TableFactor table, bool flatten = false)
    {
        return table switch
        {
            TableFactor.Table t => NormalizeTableFactor(t),
            TableFactor.Derived d => NormalizeTableFactor(d, flatten),
            TableFactor.NestedJoin n => NormalizeTableFactor(n, flatten),
            TableFactor.TableFunction function => NormalizeTableFactor(function, flatten),
            TableFactor.RowsFrom rows => NormalizeTableFactor(rows, flatten),
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
        TableFactor? relation = join.Relation != null ? NormalizeTableFactor(join.Relation, flatten) : null;
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

        if (flatten && relation is TableFactor.NestedJoin { Alias: null } nestedJoin)
        {
            relation = FlattenNestedJoin(nestedJoin, allJoins);
        }

        TableFactor normalizedRelation = NormalizeTableFactor(relation, flatten);
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
            Having = having,
            Windows = select.Windows == null
                ? null
                : [.. select.Windows.Select(window => window with
            {
                Name = ComponentNormalizer.NormalizeSingleIdentifier(window.Name),
                Specification = NormalizeWindowSpec(window.Specification, inSelect: true, flatten: flatten)
            })]
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
            case SelectBody.ValuesQuery values:
                return values with
                {
                    Values = values.Values with
                    {
                        Rows = [.. values.Values.Rows.Select(row => new SqlValueList<Expression>(row.Select(expression => NormalizeExpression(expression, inSelect: true, flatten: flatten))))]
                    }
                };
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

    private SqlValueList<SimpleSelectItem>? NormalizeCteReturning(SqlValueList<SimpleSelectItem>? selections, bool flatten)
    {
        return selections == null ? null : [.. selections.Select(selection => selection switch
        {
            SimpleSelectItem.UnnamedExpression expression => expression with { Expression = NormalizeExpression(expression.Expression, inSelect: false, flatten: flatten) },
            SimpleSelectItem.ExpressionWithAlias expression => expression with
            {
                Expression = NormalizeExpression(expression.Expression, inSelect: false, flatten: flatten),
                Alias = ComponentNormalizer.NormalizeSingleIdentifier(expression.Alias)
            },
            SimpleSelectItem.QualifiedWildcard wildcard => wildcard with { Name = ComponentNormalizer.NormalizeObjectName(wildcard.Name) },
            _ => selection
        })];
    }

    private SqlValueList<Assignment>? NormalizeCteAssignments(SqlValueList<Assignment>? assignments, bool flatten)
    {
        return assignments == null ? null : [.. assignments.Select(assignment => assignment with
        {
            Target = assignment.Target switch
            {
                AssignmentTarget.ObjectName target => target with { Name = ComponentNormalizer.NormalizeObjectName(target.Name) },
                AssignmentTarget.Tuple target => target with { Names = [.. target.Names.Select(ComponentNormalizer.NormalizeObjectName)] },
                _ => assignment.Target
            },
            Value = NormalizeExpression(assignment.Value, inSelect: false, flatten: flatten)
        })];
    }

    private PgConflictTarget? NormalizeCteConflictTarget(PgConflictTarget? target, bool flatten)
    {
        return target switch
        {
            PgConflictTarget.Constraint constraint => constraint with { Name = ComponentNormalizer.NormalizeSingleIdentifier(constraint.Name) },
            PgConflictTarget.IndexInference inference => inference with
            {
                Elements = [.. inference.Elements.Select(element => element with
                {
                    Expression = NormalizeExpression(element.Expression, inSelect: false, flatten: flatten),
                    OperatorClass = element.OperatorClass == null ? null : ComponentNormalizer.NormalizeObjectName(element.OperatorClass),
                    OperatorClassParameters = NormalizeCteAssignments(element.OperatorClassParameters, flatten)
                })],
                Where = NormalizeNullableExpression(inference.Where, inSelect: false, flatten: flatten)
            },
            _ => target
        };
    }

    private PgOnConflict? NormalizeCteConflict(PgOnConflict? conflict, bool flatten)
    {
        return conflict switch
        {
            PgOnConflict.DoNothing nothing => nothing with { Target = NormalizeCteConflictTarget(nothing.Target, flatten) },
            PgOnConflict.DoUpdate update => update with
            {
                Target = NormalizeCteConflictTarget(update.Target, flatten)!,
                Assignments = NormalizeCteAssignments(update.Assignments, flatten)!,
                Where = NormalizeNullableExpression(update.Where, inSelect: false, flatten: flatten)
            },
            _ => conflict
        };
    }

    private Statement NormalizeCteStatement(Statement query, PseudoTableSet? pseudoTables, bool returnsValues, bool flatten)
    {
        PseudoTableSet? outerPseudoTables = _pseudoTables;
        _pseudoTables = query is Select ? pseudoTables : null;
        try
        {
            return query switch
            {
                Select select => NormalizeSelect(select, topLevel: false, returnsValues, flatten: flatten),
                Insert insert => insert with
                {
                    CommonTableExpression = insert.CommonTableExpression == null ? null : NormalizeCte(insert.CommonTableExpression, flatten),
                    Name = ComponentNormalizer.NormalizeObjectName(insert.Name),
                    Alias = ComponentNormalizer.NormalizeSingleIdentifierNullable(insert.Alias),
                    Columns = NormalizeAliasColumns(insert.Columns),
                    Source = insert.Source == null ? null : NormalizeSelect(insert.Source, topLevel: false, returnsValues: false, flatten: flatten),
                    OnDuplicateKeyUpdate = NormalizeCteAssignments(insert.OnDuplicateKeyUpdate, flatten),
                    OnConflict = NormalizeCteConflict(insert.OnConflict, flatten),
                    Returning = NormalizeCteReturning(insert.Returning, flatten),
                    Output = NormalizeCteReturning(insert.Output, flatten)
                },
                Update update => update with
                {
                    CommonTableExpression = update.CommonTableExpression == null ? null : NormalizeCte(update.CommonTableExpression, flatten),
                    Table = NormalizeTableWithJoins(update.Table, flatten: flatten),
                    Assignments = NormalizeCteAssignments(update.Assignments, flatten)!,
                    From = NormalizeFrom(update.From, flatten: flatten),
                    Where = NormalizeNullableExpression(update.Where, inSelect: false, flatten: flatten),
                    Returning = NormalizeCteReturning(update.Returning, flatten),
                    Output = NormalizeCteReturning(update.Output, flatten)
                },
                Delete delete => delete with
                {
                    CommonTableExpression = delete.CommonTableExpression == null ? null : NormalizeCte(delete.CommonTableExpression, flatten),
                    From = NormalizeFrom(delete.From, flatten: flatten)!,
                    DeleteTables = delete.DeleteTables == null ? null : [.. delete.DeleteTables.Select(ComponentNormalizer.NormalizeObjectName)],
                    Using = NormalizeFrom(delete.Using, flatten: flatten),
                    Where = NormalizeNullableExpression(delete.Where, inSelect: false, flatten: flatten),
                    OrderBy = NormalizeSelectOrderBy(delete.OrderBy, inSelect: false, flatten: flatten),
                    Limit = NormalizeNullableExpression(delete.Limit, inSelect: false, flatten: flatten),
                    Returning = NormalizeCteReturning(delete.Returning, flatten),
                    Output = NormalizeCteReturning(delete.Output, flatten)
                },
                _ => throw new NotSupportedException()
            };
        }
        finally
        {
            _pseudoTables = outerPseudoTables;
        }
    }

    private (CommonTableExpressionBody, PseudoTableSet?) NormalizeCteBody(CommonTableExpressionBody cteBody, PseudoTableSet? pseudoTables, bool flatten)
    {
        INormalizeSql normalizer = ConstructNewNormalizer();
        // To account for recursive references, we need to add the CTE tables to the context before normalizing the body.
        // We'll pass a clone so that outside this function, the normalized body can be used to create the pseudo-table.
        PseudoTableSet? currPseudoTables = pseudoTables?.CloneExternalOnly();
        if (currPseudoTables != null)
        {
            cteBody.AddTablesToContext(currPseudoTables);
        }
        Statement newQuery = cteBody.Query is Select select && !cteBody.Columns.SafeAny()
            ? normalizer.NormalizeSelect(select, _activeSchema, currPseudoTables, _name, flatten: flatten)
            : NormalizeCteStatement(cteBody.Query, currPseudoTables, returnsValues: false, flatten: flatten);
        SqlValueList<Identifier>? newColumns = null;
        if (cteBody.Columns.SafeAny())
        {
            newColumns = [.. cteBody.Columns!.Select(ComponentNormalizer.NormalizeSingleIdentifier)];
        }

        CommonTableExpressionBody newBody = cteBody with
        {
            Name = ComponentNormalizer.NormalizeSingleIdentifier(cteBody.Name, _normalization.CteDeclarationNameQuotationHandling),
            Columns = newColumns,
            Query = newQuery,
            From = cteBody.From != null ? ComponentNormalizer.NormalizeSingleIdentifier(cteBody.From) : null,
            Search = cteBody.Search == null ? null : cteBody.Search with
            {
                Columns = NormalizeAliasColumns(cteBody.Search.Columns)!,
                SequenceColumn = ComponentNormalizer.NormalizeSingleIdentifier(cteBody.Search.SequenceColumn)
            },
            Cycle = cteBody.Cycle == null ? null : cteBody.Cycle with
            {
                Columns = NormalizeAliasColumns(cteBody.Cycle.Columns)!,
                MarkColumn = ComponentNormalizer.NormalizeSingleIdentifier(cteBody.Cycle.MarkColumn),
                PathColumn = ComponentNormalizer.NormalizeSingleIdentifier(cteBody.Cycle.PathColumn),
                Mark = cteBody.Cycle.Mark == null ? null : cteBody.Cycle.Mark with
                {
                    Value = NormalizeExpression(cteBody.Cycle.Mark.Value, inSelect: false, flatten: flatten),
                    DefaultValue = NormalizeExpression(cteBody.Cycle.Mark.DefaultValue, inSelect: false, flatten: flatten)
                }
            }
        };
        return (newBody, pseudoTables);
    }

    private CommonTableExpression NormalizeCte(CommonTableExpression cte, bool flatten)
    {
        PseudoTableSet? pseudoTables = _pseudoTables?.CloneExternalOnly();
        var newTables = new SqlValueList<CommonTableExpressionBody>();

        foreach (CommonTableExpressionBody table in cte.Tables)
        {
            (CommonTableExpressionBody newBody, PseudoTableSet? newPseudoTables) = NormalizeCteBody(table, pseudoTables, flatten);
            pseudoTables = newPseudoTables;
            newTables.Add(newBody);
            PseudoTable newPseudoTable = newBody.ToPseudoTable(_sourceRef, relaxed: false);
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
            Limit.OffsetFetch pagination => pagination with
            {
                Offset = NormalizeExpression(pagination.Offset, inSelect: true, flatten: flatten),
                Fetch = NormalizeNullableExpression(pagination.Fetch, inSelect: true, flatten: flatten)
            },
            Limit.PgLimit pagination => pagination with
            {
                Count = NormalizeNullableExpression(pagination.Count, inSelect: true, flatten: flatten),
                Offset = NormalizeNullableExpression(pagination.Offset, inSelect: true, flatten: flatten)
            },
            Limit.PgOffset pagination => pagination with
            {
                Offset = NormalizeExpression(pagination.Offset, inSelect: true, flatten: flatten)
            },
            Limit.PgFetch pagination => pagination with
            {
                Count = NormalizeNullableExpression(pagination.Count, inSelect: true, flatten: flatten),
                Offset = NormalizeNullableExpression(pagination.Offset, inSelect: true, flatten: flatten)
            },
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

        CommonTableExpression? cte = select.CommonTableExpression != null ? NormalizeCte(select.CommonTableExpression, flatten) : null;
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
            RowLocks = select.RowLocks?.Map(l => l with { Of = l.Of?.Map(ComponentNormalizer.NormalizeSingleIdentifier) }),
        };
    }

    private Select NormalizeSelectInternal(Select select, bool returnsValues, bool flatten = false)
    {
        select = NormalizeSelect(select, topLevel: false, returnsValues: returnsValues, flatten: flatten);
        return select;
    }

    /// <inheritdoc />
    public Select NormalizeSelect(Select selectStatement, SchemaIdentifier? activeSchema = null, PseudoTableSet? pseudoTables = null, ObjectIdentifier? name = null, SourceRef? sourceRef = null, bool flatten = true)
    {
        _name = name;
        _activeSchema = activeSchema;
        _pseudoTables = pseudoTables;
        _sourceRef = sourceRef;

        Select newSelect = NormalizeSelect(selectStatement, topLevel: true, returnsValues: true, flatten: flatten);

        _pseudoTables = null;
        _sourceRef = null;
        _activeSchema = null;
        _name = null;

        return newSelect;
    }
}
