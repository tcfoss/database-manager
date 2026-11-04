using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Components;

public abstract record RoutineParameter(DataType DataType)
{
    public abstract Identifier? Name { get; }

    public abstract StatementRoutineParameter ToStatementParameter();

    /// <summary>
    /// MySQL/MariaDB-style parameter: optional leading direction keyword
    /// (IN/OUT/INOUT), with optional MariaDB procedure defaults.
    /// </summary>
    public record Directed(Identifier ParameterName, DataType DataType, RoutineParameterDirection? Direction) : RoutineParameter(DataType)
    {
        public override Identifier Name => ParameterName;
        public Expression? DefaultExpression { get; init; }

        public override StatementRoutineParameter.Directed ToStatementParameter()
        {
            return new(Name, DataType, Direction,
                DefaultExpression == null ? null : new ParameterDefault.ExpressionDefault(DefaultExpression, true));
        }
    }

    /// <summary>
    /// T-SQL-style parameter: <c>@name TYPE [= default] [OUT|OUTPUT]</c>.
    /// No direction keyword; default value and OUTPUT marker are independent
    /// and both optional.
    /// </summary>
    public record Undirected(Identifier ParameterName, DataType DataType) : RoutineParameter(DataType)
    {
        public override Identifier Name => ParameterName;
        public Value? DefaultValue { get; init; }
        public MsRoutineParameterOptions? Options { get; init; }

        public override StatementRoutineParameter.Undirected ToStatementParameter()
        {
            return new(Name, DataType, DefaultValue == null ? null : new ParameterDefault.ValueDefault(DefaultValue, false))
            {
                Options = Options,
            };
        }
    }

    public record Postgres(Identifier? ParameterName, DataType DataType, RoutineParameterDirection Direction,
        Expression? DefaultExpression) : RoutineParameter(DataType)
    {
        public override Identifier? Name => ParameterName;

        public override StatementRoutineParameter.General ToStatementParameter()
        {
            return new StatementRoutineParameter.General(Name, DataType, Direction == RoutineParameterDirection.In ? null : Direction,
                DefaultExpression == null ? null : new ParameterDefault.ExpressionDefault(DefaultExpression, false));
        }
    }
}
