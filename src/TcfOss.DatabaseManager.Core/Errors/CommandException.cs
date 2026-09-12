using System.Text;
using TcfOss.DatabaseManager.Core.Resources;

namespace TcfOss.DatabaseManager.Core.Errors;

public class CommandException(string message, Exception? innerException = null) : InvalidOperationException(GetMessage(message), innerException)
{
    public class SpecificDialectRequired(string command)
        : CommandException(s_compositeFormat.Apply(command))
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Cmd_DialectRequired);
    }

    public class ConfigurationRequired(string command)
        : CommandException(s_compositeFormat.Apply(command))
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Cmd_ConfigurationRequired);
    }

    public class RequiredArgumentMissing(string argumentName)
        : CommandException(s_compositeFormat.Apply(argumentName))
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Cmd_RequiredArgumentMissing);
    }

    public class FileExists(string filePath)
        : CommandException(s_compositeFormat.Apply(filePath))
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Cmd_FileExists);
    }

    public class ConnectionFailed(string command)
        : CommandException(s_compositeFormat.Apply(command))
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Cmd_ConnectionFailed);
    }

    private static string GetMessage(string message)
    {
        return s_errorWithType.Apply(ErrorMessages.Err_Cmd, message);
    }

    private static readonly CompositeFormat s_errorWithType = CompositeFormat.Parse(ErrorMessages.ErrWithType);
}
