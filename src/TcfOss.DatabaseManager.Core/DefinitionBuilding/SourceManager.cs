using System.Collections.Concurrent;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.DefinitionBuilding;

public class SourceManager
{
    private int _nextSourceId;
    private readonly ConcurrentDictionary<int, SqlSource> _sources = [];
    private readonly ConcurrentDictionary<string, int> _pathToSourceId = [];
    private readonly ConcurrentDictionary<ObjectHandle, SourceRef> _objectToSourceRef = [];

    private readonly Lock _lock = new();

    public int RegisterFileSource(string path)
    {
        lock (_lock)
        {
            if (_pathToSourceId.TryGetValue(path, out int existingSourceId))
            {
                return existingSourceId;
            }

            int sourceId = _nextSourceId++;
            _sources[sourceId] = new SqlSource.FileSource(sourceId, path);
            _pathToSourceId[path] = sourceId;
            return sourceId;
        }
    }

    public void RegisterObject(ObjectHandle handle, SourceRef sourceRef)
    {
        _objectToSourceRef[handle] = sourceRef;
    }

    public SourceRef? GetSourceRef(ObjectHandle handle)
    {
        if (_objectToSourceRef.TryGetValue(handle, out SourceRef sourceRef))
        {
            return sourceRef;
        }
        return null;
    }

    public SqlSource? GetSource(int sourceId)
    {
        return _sources.GetValueOrDefault(sourceId);
    }

    public SqlSource? GetSource(ObjectHandle handle)
    {
        if (_objectToSourceRef.TryGetValue(handle, out SourceRef sourceRef))
        {
            return GetSource(sourceRef.SourceId);
        }
        return null;
    }

    private string? GetFilename(int sourceId)
    {
        SqlSource? source = GetSource(sourceId);
        if (source is SqlSource.FileSource fileSource)
        {
            return fileSource.Path;
        }
        return null;
    }

    public string? GetFilename(SourceRef sourceRef)
    {
        return GetFilename(sourceRef.SourceId);
    }

    public string? GetFilename(SourceRef? sourceRef)
    {
        return sourceRef.HasValue ? GetFilename(sourceRef.Value.SourceId) : null;
    }

    private string? GetText(int sourceId, int start, int end)
    {
        SqlSource? source = GetSource(sourceId);
        if (source is SqlSource.FileSource fileSource)
        {
            string text = File.ReadAllText(fileSource.Path);
            return text[start..end];
        }
        return null;
    }

    public string? GetText(SourceRef? sourceRef, MetaData? meta)
    {
        if (sourceRef.HasValue && meta is { Start: not null, End: not null })
        {
            return GetText(sourceRef.Value.SourceId, meta.Start.Value, meta.End.Value);
        }
        return null;
    }

    public static string? GetText(string? body, MetaData? meta)
    {
        if (body is not null && meta is { Start: not null, End: not null })
        {
            return body[meta.Start.Value..meta.End.Value];
        }
        return null;
    }
}
