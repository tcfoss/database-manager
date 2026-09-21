using System.Text;
using TcfOss.DatabaseManager.Core.Resources;
using ConfigParsing = TcfOss.DatabaseManager.Core.Configuration.Parsing;

namespace TcfOss.DatabaseManager.Core.Errors;

public class ConfigurationException(string message, Exception? innerException = null) : Exception(GetMessage(message), innerException)
{
    public class AmbiguousConfigFile(string directory, string[] fileNames)
        : ConfigurationException(s_compositeFormat.Apply(directory, string.Join(", ", fileNames.Select(name => $"'{name}'"))))
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Conf_AmbiguousConfigFile);
    }

    public class MissingFieldException(string fieldName)
        : ConfigurationException(s_compositeFormat.Apply(fieldName))
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Conf_MissingField);
    }

    public class DeployScriptMissingType(ConfigParsing.DeployScript deployScript)
        : ConfigurationException(s_compositeFormat.Apply(deployScript))
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Conf_DeployScriptMissingType);
    }

    public class DeployScriptInvalidType(ConfigParsing.DeployScript deployScript)
        : ConfigurationException(s_compositeFormat.Apply(deployScript))
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Conf_DeployScriptInvalidType);
    }

    public class InvalidFieldTypeException(string fieldName, string expectedType)
        : ConfigurationException(s_compositeFormat.Apply(fieldName, expectedType))
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Conf_InvalidFieldType);
    }

    public class InvalidNumericScaleGreaterThanPrecision(string numericType, int? precision, int? scale)
        : ConfigurationException(s_compositeFormat.Apply(numericType, scale, precision))
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Conf_Numeric_ScaleGreaterThanPrecision);
    }

    public class InvalidNumericScaleMissingPrecision(string numericType)
        : ConfigurationException(s_compositeFormat.Apply(numericType))
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Conf_Numeric_ScaleWithoutPrecision);
    }

    public class InvalidFieldOneOfException(string fieldName, object? givenValue, string[] expectedValues)
        : ConfigurationException(s_compositeFormat.Apply(fieldName, givenValue, string.Join(" | ", expectedValues)))
    {
        public InvalidFieldOneOfException(string fieldName, object? givenValue, Type enumType)
            : this(fieldName, givenValue, Enum.GetNames(enumType))
        {
        }

        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Conf_InvalidFieldOneOfValues);
    }

    public class InvalidPortException(string portValue)
        : ConfigurationException(s_compositeFormat.Apply(portValue))
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Conf_InvalidPort);
    }

    public class EnvironmentVariableNotSet(string variableName)
        : ConfigurationException(s_compositeFormat.Apply(variableName))
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Conf_EnvironmentVariableNotSet);
    }

    public class OverlappingSchemas(string conflictingFileName)
        : ConfigurationException(s_compositeFormat.Apply(conflictingFileName))
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Conf_OverlappingSchemas);
    }

    public class DefinerHostWithoutAccount()
        : ConfigurationException(ErrorMessages.Err_Conf_DefinerHostWithoutAccount);

    public class ImplicitDefiner()
        : ConfigurationException(ErrorMessages.Err_Conf_ImplicitDefiner);

    private static string GetMessage(string message)
    {
        return s_errorWithType.Apply(ErrorMessages.Err_Conf, message);
    }

    private static readonly CompositeFormat s_errorWithType = CompositeFormat.Parse(ErrorMessages.ErrWithType);
}

