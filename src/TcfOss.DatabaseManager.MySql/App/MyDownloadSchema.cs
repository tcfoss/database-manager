using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseComms;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;

namespace TcfOss.DatabaseManager.MySql.App;

public partial class MyDownloadSchema(MyConfig config, ILogger<MyDownloadSchema> logger, ILoadDbDefinition<MyDefinition> dbLoader) : IDownloadSchema
{
    private readonly MyConfig _config = config;
    private readonly ILogger<MyDownloadSchema> _logger = logger;
    private readonly ILoadDbDefinition<MyDefinition> _dbLoader = dbLoader;

    public async Task ExecuteAsync()
    {
        s_logBeginningDownload(_logger, _config.ProjectDirectory);

        MyDefinition definition = await _dbLoader.LoadDefinitionAsync();

        var formatContext = new DifferFormatManager()
        {
            QuoteStyle = _config.QuoteStyle,
            AttributeDefaults = _config.AttributeDefaults,
            Formatting = _config.DifferFormatting,
        };

        foreach ((SchemaIdentifier schemaId, MySchemaMapping schema) in _config.Schemas)
        {
            s_logSavingSchema(_logger, schemaId, schema.RootPath);

            WriteTables(schema.RootPath, definition, schemaId, formatContext);

            WriteTriggers(schema.RootPath, definition, schemaId);

            WriteFunctions(schema.RootPath, definition, schemaId);

            WriteProcedures(schema.RootPath, definition, schemaId);

            WriteViews(schema.RootPath, definition, schemaId);

            WriteEvents(schema.RootPath, definition, schemaId);
        }
    }

    private void WriteTables(string rootDir, MyDefinition definition, SchemaIdentifier schemaId, DifferFormatManager formatContext)
    {
        foreach ((ObjectHandle objectKey, MyTable table) in definition.Tables.Where(x => x.Key.Schema == schemaId.Name).OrderBy(x => x.Key.Name))
        {
            string dirPath = Path.Combine(rootDir, "Tables");
            Directory.CreateDirectory(dirPath);

            string fpath = Path.Combine(dirPath, objectKey.Name + ".sql");

            DifferFormatManager subFormatContext = formatContext with { Table = table };

            s_logWritingObject(_logger, "table", objectKey, fpath);
            CreateTable stmt = table.ToCreateStatement(_config.Formatting.ObjectNamePrefixWithSchema, subFormatContext);

            using var fout = new StreamWriter(fpath);

            fout.WriteLine($"CREATE TABLE {stmt.Name.ToSql()}");
            fout.WriteLine("(");
            int i = 0;
            foreach (StatementColumn col in stmt.Columns!)
            {
                fout.Write($"    {col.ToSql()}");
                if (i < stmt.Columns.Count - 1 || stmt.Constraints.Any())
                {
                    fout.WriteLine(",");
                }
                else
                {
                    fout.WriteLine();
                }
                i++;
            }
            fout.WriteLine("    " + stmt.Constraints.ToSqlDelimited(",\n    "));
            fout.WriteLine(")");
            if (stmt.TableOptions.Any())
            {
                fout.WriteLine($" {stmt.TableOptions.ToSqlDelimited(" ")}");
            }

            fout.WriteLine(";");
            fout.WriteLine();
        }
    }

    private void WriteTriggers(string rootDir, MyDefinition definition, SchemaIdentifier schemaId)
    {
        string dirPath = Path.Combine(rootDir, "Triggers");
        Directory.CreateDirectory(dirPath);
        foreach ((ObjectHandle objectKey, MyTrigger trigger) in definition.Triggers.Where(x => x.Key.Schema == schemaId.Name).OrderBy(x => x.Key.Name))
        {
            string fpath = Path.Combine(dirPath, objectKey.Name + ".sql");

            s_logWritingObject(_logger, "trigger", objectKey, fpath);

            using var fout = new StreamWriter(fpath);

            CreateTrigger stmt = trigger.ToCreateStatement(_config.Formatting.ObjectNamePrefixWithSchema);
            fout.WriteLine("DELIMITER //");
            fout.WriteLine();

            fout.WriteLine("CREATE");
            fout.WriteLine($"    {trigger.Definer.ToSql()}");
            fout.WriteLine($"TRIGGER {stmt.Name.ToSql()}");
            fout.WriteLine($"    {stmt.TriggerTime.ToSql()} {stmt.Events[0].ToSql()}");
            fout.WriteLine($"    ON {stmt.TableName.ToSql()}");
            fout.WriteLine("FOR EACH ROW");
            fout.WriteLine(trigger.RawBodyText + "//");
            fout.WriteLine();
            fout.WriteLine("DELIMITER ;");
        }
    }

    private void WriteFunctions(string rootDir, MyDefinition definition, SchemaIdentifier schemaId)
    {
        string dirPath = Path.Combine(rootDir, "Functions");
        Directory.CreateDirectory(dirPath);
        foreach ((ObjectHandle objectKey, MyStoredFunction function) in definition.Functions.Where(x => x.Key.Schema == schemaId.Name).OrderBy(x => x.Key.Name))
        {
            string fpath = Path.Combine(dirPath, objectKey.Name + ".sql");

            s_logWritingObject(_logger, "function", objectKey, fpath);

            using var fout = new StreamWriter(fpath);

            CreateFunction stmt = function.ToCreateStatement(_config.Formatting.ObjectNamePrefixWithSchema);
            string? aggregate = function.Aggregate ? "AGGREGATE " : null;

            fout.WriteLine("DELIMITER //");
            fout.WriteLine();

            fout.WriteLine("CREATE");
            fout.WriteLine($"    {function.Definer.ToSql()}");
            fout.WriteLine($"{aggregate}FUNCTION {stmt.Name.ToSql()} ({stmt.Parameters.ToSqlDelimited()})");
            fout.WriteLine($"    RETURNS {stmt.ReturnType.ToSql()}");
            if (stmt.MyCharacteristic != null)
            {
                fout.WriteLine($"    {stmt.MyCharacteristic.ToSql()}");
            }
            fout.WriteLine(function.RawBodyText + "//");

            fout.WriteLine();
            fout.WriteLine("DELIMITER ;");
        }
    }

    private void WriteProcedures(string rootDir, MyDefinition definition, SchemaIdentifier schemaId)
    {
        string dirPath = Path.Combine(rootDir, "Procedures");
        Directory.CreateDirectory(dirPath);
        foreach ((ObjectHandle objectKey, MyStoredProcedure procedure) in definition.Procedures.Where(x => x.Key.Schema == schemaId.Name).OrderBy(x => x.Key.Name))
        {
            string fpath = Path.Combine(dirPath, objectKey.Name + ".sql");

            s_logWritingObject(_logger, "procedure", objectKey, fpath);

            using var fout = new StreamWriter(fpath);

            CreateProcedure stmt = procedure.ToCreateStatement(_config.Formatting.ObjectNamePrefixWithSchema);

            fout.WriteLine("DELIMITER //");
            fout.WriteLine();

            fout.WriteLine("CREATE");
            fout.WriteLine($"    {procedure.Definer.ToSql()}");
            fout.WriteLine($"PROCEDURE {stmt.Name.ToSql()} ({stmt.Parameters.ToSqlDelimited()})");
            if (stmt.MyCharacteristic != null)
            {
                fout.WriteLine($"    {stmt.MyCharacteristic.ToSql()}");
            }
            fout.WriteLine(procedure.RawBodyText!.TrimStart() + "//");

            fout.WriteLine();
            fout.WriteLine("DELIMITER ;");
        }
    }

    private void WriteViews(string rootDir, MyDefinition definition, SchemaIdentifier schemaId)
    {
        string dirPath = Path.Combine(rootDir, "Views");
        Directory.CreateDirectory(dirPath);
        foreach ((ObjectHandle objectKey, MyView view) in definition.Views.Where(x => x.Key.Schema == schemaId.Name).OrderBy(x => x.Key.Name))
        {
            string fpath = Path.Combine(dirPath, objectKey.Name + ".sql");

            s_logWritingObject(_logger, "view", objectKey, fpath);

            using var fout = new StreamWriter(fpath);

            CreateView stmt = view.ToCreateStatement(_config.Formatting.ObjectNamePrefixWithSchema);
            fout.WriteLine("CREATE");
            if (view.Algorithm != null)
            {
                fout.WriteLine($"    ALGORITHM = {view.Algorithm.ToSql()}");
            }
            fout.WriteLine($"    {view.Definer.ToSql()}");
            fout.WriteLine($"    SQL SECURITY {view.SecurityContext.ToSql()}");
            fout.WriteLine($"VIEW {stmt.Name.ToSql()}");
            fout.WriteLine("AS");
            fout.WriteLine($"{view.Body.ToSql()};");
        }
    }

    private void WriteEvents(string rootDir, MyDefinition definition, SchemaIdentifier schemaId)
    {
        string dirPath = Path.Combine(rootDir, "Events");
        Directory.CreateDirectory(dirPath);
        foreach ((ObjectHandle objectKey, MyEvent evt) in definition.Events.Where(x => x.Key.Schema == schemaId.Name).OrderBy(x => x.Key.Name))
        {
            string fpath = Path.Combine(dirPath, objectKey.Name + ".sql");

            s_logWritingObject(_logger, "event", objectKey, fpath);

            using var fout = new StreamWriter(fpath);

            CreateEvent stmt = evt.ToCreateStatement(_config.Formatting.ObjectNamePrefixWithSchema);

            fout.WriteLine("DELIMITER //");
            fout.WriteLine();

            fout.WriteLine("CREATE");
            fout.WriteLine($"    {evt.Definer.ToSql()}");
            fout.WriteLine($"EVENT {stmt.Name.ToSql()}");
            fout.WriteLine($"    {stmt.Schedule.ToSql()}");
            fout.WriteLine($"    ON COMPLETION {(evt.OnCompletionPreserve ? "PRESERVE" : "NOT PRESERVE")}");
            fout.WriteLine($"    {evt.EventEnabledStatus.ToSql()}");
            if (evt.Comment != null)
            {
                fout.WriteLine($"    COMMENT {evt.Comment.ToSql()}");
            }
            fout.WriteLine("DO");
            fout.WriteLine(evt.RawBodyText + "//");

            fout.WriteLine();
            fout.WriteLine("DELIMITER ;");
        }
    }

    #region Log Delegates
    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Writing {ObjectType} '{ObjectKey}' to '{FilePath}'.")]
    private static partial void s_logWritingObject(ILogger logger, string objectType, ObjectHandle objectKey, string filePath);

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Beginning definition download to '{RootDirectory}'.")]
    private static partial void s_logBeginningDownload(ILogger logger, string rootDirectory);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Saving schema '{SchemaName}' definition to '{SchemaDir}'.")]
    private static partial void s_logSavingSchema(ILogger logger, string schemaName, string schemaDir);

    #endregion Log Delegates
}
