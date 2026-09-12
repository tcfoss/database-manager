using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;

using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Extensions;

namespace TcfOss.DatabaseManager.Core.DefinitionBuilding;

public partial class DefinitionFileLoader<TSchemaMapping> : IReadSchemaMapFiles
    where TSchemaMapping : ISchemaMapping
{
    private readonly ILogger _logger;
    private readonly ConfigWithSchemaMapsBase<TSchemaMapping> _config;
    private readonly HashSet<string> _deployScripts;

    public DefinitionFileLoader(ConfigWithSchemaMapsBase<TSchemaMapping> config, ILogger<DefinitionFileLoader<TSchemaMapping>> logger)
    {
        _config = config;
        _logger = logger;
        _deployScripts = [.. _config.Schemas.Values
                .SelectMany(s => s.DeployScripts)
                .Select(ds => ds.FilePath)];
    }

    protected virtual IEnumerable<string> GetPathList(string rootDir)
    {
        return Directory.EnumerateFiles(rootDir, "*.*", SearchOption.AllDirectories);
    }

    protected virtual string LoadText(string filePath)
    {
        return File.ReadAllText(filePath);
    }

    private IEnumerable<DefinitionFileInfo> GetSchemaFiles(ISchemaMapping schemaMap)
    {
        var matcher = new Matcher();
        if (schemaMap.IncludeFilePatterns.SafeAny())
        {
            matcher.AddIncludePatterns(schemaMap.IncludeFilePatterns);
        }
        else
        {
            matcher.AddInclude("**/*.sql");
        }
        matcher.AddExcludePatterns(schemaMap.ExcludeFilePatterns);

        string schemaRoot = schemaMap.RootPath;
        s_logLoadingFiles(_logger, schemaRoot, null);

        foreach (string filePath in GetPathList(schemaRoot))
        {
            string filePathAbs = Path.GetFullPath(filePath);
            if (_deployScripts.Contains(filePathAbs))
            {
                s_logSkippedDeployScriptFile(_logger, filePathAbs, null);
                continue;
            }

            string filePathRel = Path.GetRelativePath(schemaRoot, filePath);
            if (matcher.Match(filePathRel).HasMatches)
            {
                s_logReadingFile(_logger, filePath, null);
                yield return new DefinitionFileInfo(filePathAbs, filePathRel, LoadText(filePath));
            }
        }
    }

    public IEnumerable<DefinitionFileInfoSet> GetFiles()
    {
        foreach ((Common.SchemaIdentifier schemaId, TSchemaMapping schemaMap) in _config.Schemas)
        {
            IEnumerable<DefinitionFileInfo> files = GetSchemaFiles(schemaMap);
            yield return new DefinitionFileInfoSet(schemaId, files);
        }
    }

    public IEnumerable<DefinitionFileInfoWithSchema> GetFilesFlat()
    {
        foreach ((Common.SchemaIdentifier schemaId, TSchemaMapping schemaMap) in _config.Schemas)
        {
            IEnumerable<DefinitionFileInfo> files = GetSchemaFiles(schemaMap);
            foreach (DefinitionFileInfo file in files)
            {
                yield return new DefinitionFileInfoWithSchema(schemaId, file.FullPath, file.RelativePath, file.Text);
            }
        }
    }

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Loading files from '{SchemaRoot}'")]
    private static partial void s_logLoadingFiles(ILogger logger, string schemaRoot, Exception? ex);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "Reading file '{Path}'")]
    private static partial void s_logReadingFile(ILogger logger, string path, Exception? ex);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information, Message = "File '{Path}' is a deploy script and was skipped")]
    private static partial void s_logSkippedDeployScriptFile(ILogger logger, string path, Exception? ex);
}
