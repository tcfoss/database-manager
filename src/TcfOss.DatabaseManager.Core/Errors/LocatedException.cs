using System.Text;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Resources;

namespace TcfOss.DatabaseManager.Core.Errors;

public class LocatedException(string? message, Location? location, Exception? innerException) : Exception(message, innerException)
{
    protected Location? Location { get; } = location;

    private string? GetLocationString(string? fileName)
    {
        if (Location != null && fileName != null)
        {
            return s_fullLocation.Apply(fileName, Location.Value.Line, Location.Value.Column);
        }
        if (Location != null)
        {
            return s_locationOnly.Apply(Location.Value.Line, Location.Value.Column);
        }
        if (fileName != null)
        {
            return s_filenameOnly.Apply(fileName);
        }
        return null;
    }

    private string? GetErrorContext(string? text)
    {
        if (text == null || Location == null)
        {
            return null;
        }

        string? line = text.Split('\n').ElementAtOrDefault(Location.Value.Line - 1);
        if (line == null)
        {
            return null;
        }

        string colLine = new string(' ', Location.Value.Column - 1) + '^';

        return s_context.Apply(line, colLine);
    }

    protected string GetErrorMessage(string? fileName, string? text)
    {
        string mainMessage = s_messageWithLocation.Apply(Message, GetLocationString(fileName));
        string? context = GetErrorContext(text);

        string fullMessage = context != null
            ? s_messageWithContext.Apply(mainMessage, context)
            : mainMessage;

        return fullMessage;
    }

    protected static readonly CompositeFormat s_errorWithType = CompositeFormat.Parse(ErrorMessages.ErrWithType);

    private static readonly CompositeFormat s_filenameOnly = CompositeFormat.Parse(ErrorMessages.Err_Component_Location_FileOnly);
    private static readonly CompositeFormat s_locationOnly = CompositeFormat.Parse(ErrorMessages.Err_Component_Location_LineAndColumn);
    private static readonly CompositeFormat s_fullLocation = CompositeFormat.Parse(ErrorMessages.Err_Component_Location_Full);
    private static readonly CompositeFormat s_context = CompositeFormat.Parse(ErrorMessages.Err_Component_Context);
    private static readonly CompositeFormat s_messageWithLocation = CompositeFormat.Parse(ErrorMessages.Err_Component_MessageWithLocation);
    private static readonly CompositeFormat s_messageWithContext = CompositeFormat.Parse(ErrorMessages.Err_Component_ErrorWithContext);
}
