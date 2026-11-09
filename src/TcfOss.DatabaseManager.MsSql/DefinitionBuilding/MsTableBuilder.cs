using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;
using TcfOss.DatabaseManager.MsSql.Configuration;
using TcfOss.DatabaseManager.MsSql.DatabaseObjects;
using TcfOss.DatabaseManager.MsSql.DatabaseObjects.Components;

namespace TcfOss.DatabaseManager.MsSql.DefinitionBuilding;

public class MsTableBuilder(ObjectIdentifier name, MsConfig config, INormalizeComponents componentNormalizer, INormalizeSql expressionNormalizer, SourceRef? sourceRef)
    : TableBuilder<MsTable>(name, config.NameHandling, sourceRef)
{
    private readonly SqlValueList<MsColumn> _columns = [];
    
    private readonly QuoteStyle _quoteStyle = config.QuoteStyle;

    private void AddColumn(MsColumn column, SourceRef? sourceRef)
    {
        if (!ColumnNames.Add(column.Name.Name))
        {
            throw new SqlSyntaxException.DuplicateColumn(column.Name, sourceRef);
        }
        _columns.Add(column);
    }

    public override void AddColumn(StatementColumn column, SourceRef? sourceRef)
    {
        
        throw new NotImplementedException();
    }

    public override void AddIndexOrConstraint(StatementTableConstraint constraint, SourceRef? sourceRef) => throw new NotImplementedException();
    public override void AddOption(StatementTableOption tableOption) => throw new NotImplementedException();
    public override PseudoTable ToPseudoTable() => throw new NotImplementedException();
    public override MsTable ToTable()
    {
        return new MsTable(Name, _columns);
    }

    protected override DataType GetNormalizedDataType(DataType dataType) => throw new NotImplementedException();
    protected override Expression GetNormalizedExpression(Expression expression) => throw new NotImplementedException();
    protected override (Expression Normalized, Expression Flattened) GetNormalizedExpressions(Expression expression) => throw new NotImplementedException();
}
