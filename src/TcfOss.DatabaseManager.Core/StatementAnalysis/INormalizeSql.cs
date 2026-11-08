using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.StatementAnalysis;

public interface INormalizeSql
{
    /// <summary>
    /// Normalizes an expression for AST comparison, removing explicit parentheses.
    /// </summary>
    /// <remarks>
    /// The result is not intended for SQL emission: removing parentheses may change grouping when printed.
    /// Use <see cref="NormalizeExpressionNoFlatten"/> when the normalized expression must remain printable.
    /// </remarks>
    public Expression NormalizeExpression(Expression expr);

    /// <summary>
    /// Normalizes an expression while preserving explicit parentheses for SQL emission.
    /// </summary>
    public Expression NormalizeExpressionNoFlatten(Expression expr);

    /// <summary>
    /// Normalizes a SELECT statement for AST comparison, flattening explicit parentheses.
    /// </summary>
    /// <remarks>
    /// The result is not intended for SQL emission: removing parentheses may change grouping when printed.
    /// </remarks>
    public Select NormalizeSelect(Select selectStatement, SchemaIdentifier? activeSchema = null, PseudoTableSet? pseudoTables = null, ObjectIdentifier? name = null, SourceRef? sourceRef = null);
}
