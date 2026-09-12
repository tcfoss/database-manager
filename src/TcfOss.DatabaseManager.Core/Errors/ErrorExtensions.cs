using System.Globalization;
using System.Text;

namespace TcfOss.DatabaseManager.Core.Errors;

public static class ErrorExtensions
{
    public static string Apply(this CompositeFormat format, params object?[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, format, args);
    }
}
