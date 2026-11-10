using System.Runtime.CompilerServices;
using TcfOss.DatabaseManager.Core.Formatting;

namespace TcfOss.DatabaseManager.Core.IO;

#pragma warning disable IDE0060 // Remove unused parameter
#pragma warning disable CA1822 // Mark members as static

public class DelimitedWriter(SqlTextWriter writer, string delimiter)
{
    public SqlTextWriter Writer => writer;
    private readonly string _delimiter = delimiter;
    private bool _first = true;

    internal void BeginItem()
    {
        if (!_first)
        {
            Writer.Write(_delimiter);
        }
        _first = false;
    }

    public void WriteSql([InterpolatedStringHandlerArgument("")] ref SqlInterpolatedStringHandler handler)
    {
        // Handled by `handler`
    }

    public void Write(string? value)
    {
        BeginItem();
        Writer.Write(value);
    }

    public void FormatSql(IWriteSql value, FormatManager manager)
    {
        BeginItem();
        value.FormatSql(Writer, manager);
    }
}
