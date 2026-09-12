
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
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

    public RoutineParameter NormalizeRoutineParameter(RoutineParameter parameter, bool isFunction)
    {
        RoutineParameterDirection? direction = parameter is RoutineParameter.Directed d ? d.Direction : null;
        return new RoutineParameter.Directed(
            _componentNormalizer.NormalizeSingleIdentifier(parameter.Name),
            _dataTypeNormalizer.NormalizeDataType(parameter.DataType),
            direction ?? (isFunction ? _config.AttributeDefaults.FunctionParameterDirection : _config.AttributeDefaults.ProcedureParameterDirection)
        );
    }
}
