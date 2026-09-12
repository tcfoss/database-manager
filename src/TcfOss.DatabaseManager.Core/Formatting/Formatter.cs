using System.Text;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.Core.Formatting;

public class Formatter : IDisposable, IFormatSql
{
    private readonly FormatManager _context;
    private readonly IParseText _parser;
    private readonly SqlTextWriter _writer;

    /// <summary>Creates a formatter using the given configuration and parser.</summary>
    public Formatter(ConfigBase config, IParseText parser, IFunctionNameProvider functionNameProvider)
    {
        var indenter = new Indenter(config.Formatting.TabSize, config.Formatting.PreferTabs);
        _context = new FormatManager
        {
            Formatting = config.Formatting,
            FunctionNameProvider = functionNameProvider,
            ComponentNormalizer = new ComponentNormalizer(config.QuoteStyle, functionNameProvider),
            Indenter = indenter
        };
        _parser = parser;
        _writer = new SqlTextWriter(new StringBuilder(), indenter);
    }

    /// <summary>Appends the formatted representation of <paramref name="statement"/> to the internal buffer.</summary>
    public void Format(Statement statement)
    {
        statement.FormatSql(_writer, _context);
        _context.TerminateStatement(_writer, statement);
    }

    /// <summary>Parses <paramref name="sqlText"/> and appends each statement's formatted representation to the internal buffer.</summary>
    public void Format(string sqlText)
    {
        foreach (Statement stmt in _parser.ParseText(sqlText))
        {
            Format(stmt);
        }
    }

    /// <summary>
    /// Appends the formatted representation of <paramref name="statement"/> to the internal buffer
    /// and returns the full buffer contents, then clears the buffer.
    /// </summary>
    public string GetFormatted(Statement statement)
    {
        Format(statement);
        string result = _writer.ToString().TrimEnd();
        Clear();
        return result;
    }

    /// <summary>
    /// Returns the full buffer contents as a formatted string and clears the buffer.
    /// If <paramref name="sqlText"/> is provided, it is parsed and appended to the buffer first.
    /// </summary>
    /// <remarks>Call with no argument to flush output accumulated via <see cref="Format(string)"/>.</remarks>
    public string GetFormatted(string? sqlText = null)
    {
        if (sqlText != null)
        {
            Format(sqlText);
        }
        string result = _writer.ToString().TrimEnd();
        Clear();
        return result;
    }

    /// <summary>Optional pseudo-table set used to rewrite unqualified column references during formatting.</summary>
    public PseudoTableSet? PseudoTables
    {
        get => _context.PseudoTables;
        set => _context.PseudoTables = value;
    }

    /// <summary>Clears the internal buffer and resets all formatting state, ready for the next use.</summary>
    public void Clear()
    {
        _writer.GetStringBuilder().Clear();
        _context.Indenter.ResetIndent(0);
        _context.ExpectingNewLineBeforeStatement = false;
    }

    public void Dispose()
    {
        _writer.Dispose();
        GC.SuppressFinalize(this);
    }
}
