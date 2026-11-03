using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.DefinitionBuilding;

public abstract class TableBuilder<TTable>(ObjectIdentifier name, NameHandling nameHandling, INormalizeComponents componentNormalizer, SourceRef? sourceRef)
{
    protected ObjectIdentifier Name { get; } = name;
    protected NameHandling NameHandling { get; } = nameHandling;
    protected INormalizeComponents ComponentNormalizer { get; } = componentNormalizer;
    protected SourceRef? SourceRef { get; } = sourceRef;
    protected HashSet<string> ColumnNames { get; } = [];

    public void ApplyCreateTable(CreateTable createTable)
    {
        foreach (StatementTableOption opt in createTable.TableOptions)
        {
            AddOption(opt);
        }
        if (createTable.Columns.SafeAny())
        {
            foreach (StatementColumn column in createTable.Columns)
            {
                // TODO: Add source info to column and constraint statements.
                AddColumn(column, SourceRef);
            }
        }
        if (createTable.Constraints.SafeAny())
        {
            foreach (StatementTableConstraint constraint in createTable.Constraints)
            {
                AddIndexOrConstraint(constraint, SourceRef);
            }
        }
    }

    protected ColumnOption.ColumnDefault NormalizeDefault(StatementColumnOption.Default givenDefault)
    {
        Identifier? name = ComponentNormalizer.NormalizeSingleIdentifierNullable(givenDefault.Name);
        switch (givenDefault)
        {
            case StatementColumnOption.Default.DefaultExpression { Expression: LiteralValue literal }:
                return new ColumnOption.ColumnDefault.DefaultValue(ComponentNormalizer.NormalizeValue(literal.Value), name);
            case StatementColumnOption.Default.DefaultValue value:
                return new ColumnOption.ColumnDefault.DefaultValue(ComponentNormalizer.NormalizeValue(value.Value), name);
            case StatementColumnOption.Default.DefaultExpression expression:
                (Expression normalized, Expression flattened) = GetNormalizedExpressions(expression.Expression);
                return new ColumnOption.ColumnDefault.DefaultExpression(normalized, name)
                {
                    NormalizedExpression = flattened
                };
            default:
                throw new DefensiveGuardException(true, "column default type", givenDefault.GetType());
        }
    }

    // ReSharper disable MemberCanBeProtected.Global
    // ReSharper disable UnusedMemberInSuper.Global
    public abstract void AddColumn(StatementColumn column, SourceRef? sourceRef);
    public abstract void AddIndexOrConstraint(StatementTableConstraint constraint, SourceRef? sourceRef);
    public abstract void AddOption(StatementTableOption tableOption);

    public abstract PseudoTable ToPseudoTable();

    public abstract TTable ToTable();

    protected abstract DataType GetNormalizedDataType(DataType dataType);
    protected abstract Expression GetNormalizedExpression(Expression expression);
    protected abstract (Expression Normalized, Expression Flattened) GetNormalizedExpressions(Expression expression);
    // ReSharper restore MemberCanBeProtected.Global
    // ReSharper restore UnusedMemberInSuper.Global
}
