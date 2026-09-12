using System.Globalization;
using TcfOss.DatabaseManager.Core.Resources;

namespace TcfOss.DatabaseManager.Core.Tests;

public static class MessageTemplates
{
    public static readonly string ConfigRequiredTemplate = string.Format(CultureInfo.CurrentCulture, ErrorMessages.ErrWithType, ErrorMessages.Err_Cmd, ErrorMessages.Err_Cmd_ConfigurationRequired);
    public static readonly string ConnectionRequiredTemplate = string.Format(CultureInfo.CurrentUICulture, ErrorMessages.ErrWithType, ErrorMessages.Err_Cmd, ErrorMessages.Err_Cmd_ConnectionFailed);
    public static readonly string DialectRequiredTemplate = string.Format(CultureInfo.CurrentCulture, ErrorMessages.ErrWithType, ErrorMessages.Err_Cmd, ErrorMessages.Err_Cmd_DialectRequired);
    public static readonly string ProcNoDefinerTemplate = string.Format(CultureInfo.CurrentCulture, ErrorMessages.ErrWithType, ErrorMessages.Err_Def, ErrorMessages.Err_Def_ObjectNoDefiner);
    public static readonly string FkBackingIndexTemplate = string.Format(CultureInfo.CurrentCulture, ErrorMessages.ErrWithType, ErrorMessages.Err_Def, ErrorMessages.Err_Def_ForeignKeyNoBackingIndex);
    public static readonly string FileExistsTemplate = string.Format(CultureInfo.CurrentCulture, ErrorMessages.ErrWithType, ErrorMessages.Err_Cmd, ErrorMessages.Err_Cmd_FileExists);
    public static readonly string IndexColumnNotFoundTemplate = string.Format(CultureInfo.CurrentCulture, ErrorMessages.ErrWithType, ErrorMessages.Err_Syntax, ErrorMessages.Err_Syntax_IndexColumnNotFound);
    public static readonly string NoDefinerTemplate = string.Format(CultureInfo.CurrentCulture, ErrorMessages.ErrWithType, ErrorMessages.Err_Def, ErrorMessages.Err_Def_ObjectNoDefiner);
    public static readonly string ImplicitDefinerTemplate = string.Format(CultureInfo.CurrentCulture, ErrorMessages.ErrWithType, ErrorMessages.Err_Def, ErrorMessages.Err_Def_ObjectImplicitDefiner);
    public static readonly string SelectAsTableTemplate = string.Format(CultureInfo.CurrentCulture, ErrorMessages.ErrWithType, ErrorMessages.Err_Def, ErrorMessages.Err_Def_Table_SelectAs);
    public static readonly string NoSqlSecurityTemplate = string.Format(CultureInfo.CurrentCulture, ErrorMessages.ErrWithType, ErrorMessages.Err_Def, ErrorMessages.Err_Def_ObjectNoSecurityContext);
    public static readonly string TriggerOrderViaPrecedesTemplate = string.Format(CultureInfo.CurrentCulture, ErrorMessages.ErrWithType, ErrorMessages.Err_Def, ErrorMessages.Err_Def_Trigger_PrecedesUnsupported);
    public static readonly string TriggerOrderUndefinedTemplate = string.Format(CultureInfo.CurrentCulture, ErrorMessages.ErrWithType, ErrorMessages.Err_Def, ErrorMessages.Err_Def_Trigger_UndefinedOrder);
}
