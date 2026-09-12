using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.StatementAnalysis;

public interface INormalizeSql
{
    public Expression NormalizeExpression(Expression expr);
    public Expression NormalizeExpressionNoFlatten(Expression expr);
    public Select NormalizeSelect(Select selectStatement, SchemaIdentifier? activeSchema = null, PseudoTableSet? pseudoTables = null, ObjectIdentifier? name = null, SourceRef? sourceRef = null);
}
