using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.MySql.Configuration;

namespace TcfOss.DatabaseManager.MySql.DefinitionBuilding;

public class MyAttributeNormalizer(MyConfig config, INormalizeComponents componentNormalizer, INormalizeDataTypes dataTypeNormalizer)
{
    private readonly MyConfig _config = config;
    private readonly INormalizeComponents _componentNormalizer = componentNormalizer;
    private readonly INormalizeDataTypes _dataTypeNormalizer = dataTypeNormalizer;
    private readonly ExtendedQuoteStyle _accountQuoteStyle = config.NormalizationSettings.AccountQuoteStyle;

    public Account NormalizeAccount(Account account, ExtendedQuoteStyle? quoteStyle)
    {
        quoteStyle ??= _accountQuoteStyle;

        return account switch
        {
            Account.Identity id => new Account.Identity(new ExtendedIdentifier(id.Name.Name, quoteStyle.Value)),
            Account.IdentityWithHost idh => new Account.IdentityWithHost(new ExtendedIdentifier(idh.Name.Name, quoteStyle.Value), new ExtendedIdentifier(idh.Host.Name, quoteStyle.Value)),
            Account.CurrentUser or Account.CurrentRole or Account.SessionUser or Account.Identity or Account.IdentityWithHost => account,
            _ => throw new InvalidOperationException($"Unknown account type {account.GetType()}.")
        };
    }

    public RoutineParameter NormalizeRoutineParameter(StatementRoutineParameter parameter, bool isFunction)
    {
        Identifier name = parameter.Name
            ?? throw new SqlSyntaxException.UnsupportedDialect("MySQL/MariaDB", "unnamed routine parameters");
        RoutineParameterDirection? direction = parameter switch
        {
            StatementRoutineParameter.Directed directed => directed.Direction,
            StatementRoutineParameter.General general => general.Direction,
            _ => null
        };
        Expression? defaultExpression = parameter switch
        {
            StatementRoutineParameter.Directed { Default: ParameterDefault.ExpressionDefault expression } => expression.Expression,
            StatementRoutineParameter.Directed { Default: ParameterDefault.ValueDefault value } => new LiteralValue(value.Value),
            StatementRoutineParameter.General { Default: ParameterDefault.ExpressionDefault expression } => expression.Expression,
            StatementRoutineParameter.General { Default: ParameterDefault.ValueDefault value } => new LiteralValue(value.Value),
            _ => null
        };
        return new RoutineParameter.Directed(
            _componentNormalizer.NormalizeSingleIdentifier(name),
            _dataTypeNormalizer.NormalizeDataType(((DataTypeReference.BuiltIn)parameter.DataType).DataType),
            direction ?? (isFunction ? _config.AttributeDefaults.FunctionParameterDirection : _config.AttributeDefaults.ProcedureParameterDirection)
        )
        {
            DefaultExpression = defaultExpression
        };
    }
}
