using System.Text;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Resources;
using TcfOss.DataStructures.Enums;

namespace TcfOss.DatabaseManager.Core.Errors;

public abstract class ParseException(string message, Location? location, Exception? innerException = null) : LocatedException(message, location, innerException)
{
    public class DialectRequired(string syntax, Location? location = null)
        : ParseException(s_compositeFormat.Apply(syntax), location)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Parse_DialectRequired);
    }

    public class ColumnComponentInvalidName(string componentType, string givenName, Location? location = null)
        : ParseException(s_compositeFormat.Apply(componentType, givenName), location)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Parse_ColumnComponentUnexpectedName);
    }

    public class Duplicate(string attributeName, Location? location = null)
        : ParseException(s_compositeFormat.Apply(attributeName), location)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Parse_DuplicateSpecification);
    }

    public class ExpectedButFound(string expected, string found, Location? location = null)
        : ParseException(s_compositeFormat.Apply(expected, found), location)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Parse_ExpectedButFound);
    }

    public class ExpectedOneOfButFound(IEnumerable<string> expected, string found, Location? location = null)
        : ParseException(s_compositeFormat.Apply(string.Join(" | ", expected), found), location)
    {
        public static ExpectedOneOfButFound FromEnumLike<T>(string found, Location? location = null)
            where T : IStringEnum<T>
        {
            string[] expected = [.. T.AllowedValues];
            return new ExpectedOneOfButFound(expected, $"{found}", location);
        }

        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Parse_ExpectedOneOfButFound);
    }

    public class ValueOutOfRange(string label, string value, long minimum, long maximum, Location? location = null)
        : ParseException(s_compositeFormat.Apply(label, value, minimum, maximum), location)
    {
        public string Label { get; } = label;
        public string Value { get; } = value;
        public long Minimum { get; } = minimum;
        public long Maximum { get; } = maximum;

        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Parse_ValueOutOfRange);
    }

    public class InvalidIntervalFieldRange(string startField, string endField, Location? location = null)
        : ParseException(s_compositeFormat.Apply(startField, endField), location)
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Parse_InvalidIntervalFieldRange);
    }

    public class IntervalPrecisionRequiresSecond(Location? location = null)
        : ParseException(ErrorMessages.Err_Parse_IntervalPrecisionRequiresSecond, location);

    public class WithText(ParseException originalException, string? filename, string? text) : ParseException(GetMessage(originalException, filename, text), originalException.Location, originalException)
    {
        private static string GetMessage(ParseException originalException, string? filename, string? text)
        {
            return s_errorWithType.Apply(ErrorMessages.Err_Parse, originalException.GetErrorMessage(filename, text));
        }
    }
}
