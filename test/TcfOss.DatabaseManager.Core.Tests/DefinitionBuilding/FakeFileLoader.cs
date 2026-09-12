using Microsoft.Extensions.Logging;

using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;

namespace TcfOss.DatabaseManager.Core.Tests.DefinitionBuilding;

public class FakeFileLoader<TSchemaMapping>(ConfigWithSchemaMapsBase<TSchemaMapping> config, ILogger<DefinitionFileLoader<TSchemaMapping>> logger, List<string> files, Func<string, string> getTextFunc) : DefinitionFileLoader<TSchemaMapping>(config, logger)
    where TSchemaMapping : ISchemaMapping
{
    private readonly List<string> _files = files;
    private readonly Func<string, string> _getTextFunc = getTextFunc;

    public FakeFileLoader(ConfigWithSchemaMapsBase<TSchemaMapping> config, ILogger<DefinitionFileLoader<TSchemaMapping>> logger)
        : this(config, logger, TestDataFileSystem.Files, TestDataFileSystem.GetText)
    { }

    protected override IEnumerable<string> GetPathList(string rootDir)
    {
        foreach (var filePath in _files)
        {
            yield return filePath;
        }
    }

    protected override string LoadText(string filePath)
    {
        return _getTextFunc(filePath);
    }
}
