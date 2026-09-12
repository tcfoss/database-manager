using System.Text;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Resources;

namespace TcfOss.DatabaseManager.Core.Errors;

public class ConfigParseException(string message, Location? location, Exception? innerException = null)
    : LocatedException(message, location, innerException)
{
    public class UnknownField(string fieldName, Location? location, Exception? innerException = null)
        : ConfigParseException(s_compositeFormat.Apply(fieldName), location, innerException)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Conf_UnknownField);
    }

    public class RequestedValueNotFound(string fieldName, string requestedValue, Location? location, Exception? innerException = null)
        : ConfigParseException(s_compositeFormat.Apply(requestedValue, fieldName), location, innerException)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Conf_RequestedValueNotFound);
    }

    public class InvalidValue(string fieldName, string value, Location? location, Exception? innerException = null)
        : ConfigParseException(s_compositeFormat.Apply(value, fieldName), location, innerException)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Conf_InvalidValue);
    }

    public class InvalidNull(string fieldName, Location? location, Exception? innerException = null)
        : ConfigParseException(s_compositeFormat.Apply(fieldName), location, innerException)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Conf_InvalidNull);
    }

    public class WithText(ConfigParseException originalException, string? filename, string? text) : ConfigParseException(GetMessage(originalException, filename, text), originalException.Location, originalException)
    {
        private static string GetMessage(ConfigParseException originalException, string? filename, string? text)
        {
            return s_errorWithType.Apply(ErrorMessages.Err_Conf, originalException.GetErrorMessage(filename, text));
        }
    }
}
