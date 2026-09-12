using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.DefinitionMapping;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.Core.IO;

public class DifferStatementWriter
{
    private readonly DifferFormattingSettings _settings;
    private readonly WriteOptions _writeOptions;
    private readonly WriteOptions _procedureOptions;
    private readonly WriteOptions _functionOptions;
    private readonly WriteOptions _triggerOptions;
    private readonly WriteOptions _viewOptions;
    private readonly WriteOptions _eventOptions;
    private readonly WriteOptions _procedureOptionsScript;
    private readonly WriteOptions _functionOptionsScript;
    private readonly WriteOptions _triggerOptionsScript;
    private readonly WriteOptions _viewOptionsScript;
    private readonly WriteOptions _eventOptionsScript;
    private readonly TextWriter _writer;
    private SchemaIdentifier? _lastSchema;
    private static readonly string[] s_programDelimiterOptions = ["//", "$$", "///", "$$$"];

    public DifferStatementWriter(DifferFormattingSettings settings, TextWriter? writer = null)
    {
        _settings = settings;
        _writer = writer ?? Console.Out;
        _writeOptions = new WriteOptions
        {
            TerminateStatements = _settings.TerminateStatements,
            UseDelimiterAroundPrograms = _settings.UseDelimiterAroundPrograms,
        };
        _procedureOptions = new WriteOptions
        {
            TerminateStatements = _settings.TerminateStatements,
            UseDelimiterAroundPrograms = _settings.UseDelimiterAroundPrograms,
            PreferRawText = _settings.ProcedurePreferRawText
        };
        _functionOptions = new WriteOptions
        {
            TerminateStatements = _settings.TerminateStatements,
            UseDelimiterAroundPrograms = _settings.UseDelimiterAroundPrograms,
            PreferRawText = _settings.FunctionPreferRawText
        };
        _triggerOptions = new WriteOptions
        {
            TerminateStatements = _settings.TerminateStatements,
            UseDelimiterAroundPrograms = _settings.UseDelimiterAroundPrograms,
            PreferRawText = _settings.TriggerPreferRawText
        };
        _viewOptions = new WriteOptions
        {
            TerminateStatements = _settings.TerminateStatements,
            UseDelimiterAroundPrograms = _settings.UseDelimiterAroundViews,
            PreferRawText = _settings.ViewPreferRawText
        };
        _eventOptions = new WriteOptions
        {
            TerminateStatements = _settings.TerminateStatements,
            UseDelimiterAroundPrograms = _settings.UseDelimiterAroundPrograms,
            PreferRawText = _settings.EventPreferRawText
        };
        _procedureOptionsScript = new WriteOptions
        {
            TerminateStatements = _settings.TerminateStatements,
            UseDelimiterAroundPrograms = _settings.UseDelimiterAroundPrograms,
            PreferRawText = _settings.ProcedurePreferRawTextInScript
        };
        _functionOptionsScript = new WriteOptions
        {
            TerminateStatements = _settings.TerminateStatements,
            UseDelimiterAroundPrograms = _settings.UseDelimiterAroundPrograms,
            PreferRawText = _settings.FunctionPreferRawTextInScript
        };
        _triggerOptionsScript = new WriteOptions
        {
            TerminateStatements = _settings.TerminateStatements,
            UseDelimiterAroundPrograms = _settings.UseDelimiterAroundPrograms,
            PreferRawText = _settings.TriggerPreferRawTextInScript
        };
        _viewOptionsScript = new WriteOptions
        {
            TerminateStatements = _settings.TerminateStatements,
            UseDelimiterAroundPrograms = _settings.UseDelimiterAroundViews,
            PreferRawText = _settings.ViewPreferRawTextInScript
        };
        _eventOptionsScript = new WriteOptions
        {
            TerminateStatements = _settings.TerminateStatements,
            UseDelimiterAroundPrograms = _settings.UseDelimiterAroundPrograms,
            PreferRawText = _settings.EventPreferRawTextInScript
        };
    }

    public void WriteDefinitionAlter(DefinitionAlterStatement definitionAlterStatement)
    {
        _writer.WriteLine();

        if (_writeOptions.UseCatalogOnChange && definitionAlterStatement.Schema.Catalog != _lastSchema?.Catalog)
        {
            _writer.WriteLine($"USE {definitionAlterStatement.Schema.Catalog.ToSimpleIdentifier()};");
        }
        else if (_writeOptions.UseSchemaOnChange && definitionAlterStatement.Schema != _lastSchema)
        {
            _writer.WriteLine($"USE {definitionAlterStatement.Schema.ToSimpleIdentifier()};");
        }
        _lastSchema = definitionAlterStatement.Schema;

        if (definitionAlterStatement.Comment != null && _settings.EmitCommentsWithWeights)
        {
            _writer.WriteLine($"/* {definitionAlterStatement.Comment} ({definitionAlterStatement.Weight}) */");
        }

        WriteStatement(definitionAlterStatement.Statement, definitionAlterStatement.FromDeployScript);
    }

    public void WriteStatement(Statement statement, bool fromDeployScript = false)
    {
        if (statement is StatementGroup group)
        {
            foreach (Statement subStatement in group.Substatements)
            {
                WriteStatement(subStatement, fromDeployScript);
            }
            return;
        }

        WriteOptions? options = statement switch
        {
            CreateProcedure => fromDeployScript ? _procedureOptionsScript : _procedureOptions,
            CreateFunction => fromDeployScript ? _functionOptionsScript : _functionOptions,
            CreateTrigger => fromDeployScript ? _triggerOptionsScript : _triggerOptions,
            CreateView => fromDeployScript ? _viewOptionsScript : _viewOptions,
            CreateEvent => fromDeployScript ? _eventOptionsScript : _eventOptions,
            _ => null
        };

        if (options != null)
        {
            WriteProgramStatement(statement, options);
        }
        else
        {
            WriteNonProgramStatement(statement);
        }
        _writer.WriteLine();
    }

    private void WriteProgramStatement(Statement statement, WriteOptions options)
    {
        string delimiter = ";";
        string sql = statement.ToSql(options);

        if (options.UseDelimiterAroundPrograms)
        {
            delimiter = s_programDelimiterOptions.First(d => !sql.Contains(d));
        }

        if (delimiter != ";")
        {
            _writer.WriteLine($"DELIMITER {delimiter}");
        }

        _writer.Write(sql);

        if (options.TerminateStatements)
        {
            _writer.Write(delimiter);
        }

        if (delimiter != ";")
        {
            _writer.WriteLine();
            _writer.WriteLine("DELIMITER ;");
        }
    }

    private void WriteNonProgramStatement(Statement statement)
    {
        string sql = statement.ToSql(_writeOptions);
        _writer.Write(sql);
        if (_writeOptions.TerminateStatements && !sql.TrimEnd().EndsWith(';'))
        {
            _writer.Write(";");
        }
    }
}
