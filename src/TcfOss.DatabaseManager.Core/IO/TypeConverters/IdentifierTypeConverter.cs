using System.ComponentModel;
using TcfOss.DatabaseManager.Core.IO.JsonConverters;

namespace TcfOss.DatabaseManager.Core.IO.TypeConverters;

public class IdentifierTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object value)
    {
        if (value is string strValue)
        {
            return IdentifierConverter.Parse(strValue);
        }
        return base.ConvertFrom(context, culture, value);
    }
}
