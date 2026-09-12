namespace TcfOss.DatabaseManager.Core.DefinitionBuilding;

// Interface also provided for library usage
// ReSharper disable UnusedMemberInSuper.Global
public interface IReadSchemaMapFiles
{
    /// <summary>
    /// Based on the project configuration, iterate over all files
    /// under the schema map directories, returning the file paths
    /// and contents.
    /// </summary>
    /// <returns></returns>
    public IEnumerable<DefinitionFileInfoSet> GetFiles();

    public IEnumerable<DefinitionFileInfoWithSchema> GetFilesFlat();
}
