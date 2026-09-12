using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.DefinitionMapping;

public interface IComputeChanges
{
    public Task ExecuteAsync(string? outputFilePath, FileExistsAction fileExistsAction, CancellationToken cancellationToken = default);
}
