using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;
using TcfOss.DatabaseManager.Postgres.BuiltIn;
using TcfOss.DatabaseManager.Postgres.Configuration;
using TcfOss.DatabaseManager.Postgres.DatabaseObjects;

namespace TcfOss.DatabaseManager.Postgres.DefinitionBuilding;

public class PgTypeResolutionManager(PgConfig config, IReadOnlyDictionary<ObjectHandle, ObjectIdentifier> names,
    INormalizeComponents componentNormalizer)
{
    private readonly PgConfig _config = config;
    private readonly Dictionary<ObjectHandle, ObjectIdentifier> _names = names.ToDictionary(entry => entry.Key, entry => componentNormalizer.NormalizeObjectIdentifier(entry.Value));
    private readonly Identifier[] _searchPath = [.. config.SearchPath];

    public PgTypeResolutionManager(PgConfig config, IReadOnlyDictionary<ObjectHandle, ObjectIdentifier> names)
        : this(config, names, new ComponentNormalizer(config.QuoteStyle, new FunctionNameProvider(), config.NameHandling))
    {
    }

    public ObjectIdentifier GetName(ObjectHandle handle)
    {
        return _names[handle];
    }

    public ObjectIdentifier GetName(ObjectIdentifier name)
    {
        ObjectHandle handle = name.OriginalIdentifiers != null
            ? ObjectHandle.Create(name.OriginalIdentifiers, name.Schema, _config.NameHandling)
            : ObjectHandle.Create(name, _config.NameHandling);
        return GetName(handle);
    }

    public DataType Resolve(DataTypeReference reference, SchemaIdentifier activeSchema, SourceRef? sourceRef = null)
    {
        if (reference is DataTypeReference.Array array)
        {
            return new PgDataType.Array(Resolve(array.ElementType, activeSchema, sourceRef));
        }
        if (reference is DataTypeReference.BuiltIn builtIn)
        {
            return builtIn.DataType;
        }

        var named = (DataTypeReference.Named)reference;
        if (named.Name.Values.Count > 1)
        {
            ObjectHandle handle = ObjectHandle.Create(named.Name, activeSchema, _config.NameHandling);
            if (_names.TryGetValue(handle, out ObjectIdentifier? name))
            {
                return new PgDataType.Custom(name);
            }
        }
        else
        {
            foreach (Identifier schema in _searchPath)
            {
                var candidateSchema = new SchemaIdentifier(schema.Name, activeSchema.Catalog, schema.QuoteStyle);
                ObjectHandle handle = ObjectHandle.Create(named.Name, candidateSchema, _config.NameHandling);
                if (_names.TryGetValue(handle, out ObjectIdentifier? name))
                {
                    return new PgDataType.Custom(name);
                }
            }
        }

        SourceRef? location = named.Name.Source;
        if (location != null && sourceRef != null)
        {
            location = location.Value with { SourceId = sourceRef.Value.SourceId };
        }
        SchemaIdentifier[] searchedSchemas = named.Name.Values.Count > 1
            ? [SchemaIdentifier.FromObjectName(new ObjectName([.. named.Name.Values[..^1]]), activeSchema.Catalog, null)]
            : [.. _searchPath.Select(schema => new SchemaIdentifier(schema.Name, activeSchema.Catalog, schema.QuoteStyle))];
        throw new SqlSyntaxException.TypeNotFound(named.Name, searchedSchemas, location ?? sourceRef);
    }

    public void ValidateDependencies(PgDefinition definition, SourceManager sources)
    {
        var dependencies = _names.Keys.ToDictionary(handle => handle, _ => new HashSet<ObjectHandle>());
        foreach (KeyValuePair<ObjectHandle, PgCustomType> type in definition.CustomTypes)
        {
            IEnumerable<DataType> references = type.Value switch
            {
                PgCustomType.Domain domain => [domain.BaseType],
                PgCustomType.Composite composite => composite.Attributes.Select(field => field.DataType),
                _ => []
            };
            AddDependencies(type.Key, references);
        }
        foreach (KeyValuePair<ObjectHandle, PgTable> table in definition.Tables)
        {
            AddDependencies(table.Key, table.Value.Columns.Select(column => column.DataType));
        }

        var dependents = _names.Keys.ToDictionary(handle => handle, _ => new List<ObjectHandle>());
        foreach (KeyValuePair<ObjectHandle, HashSet<ObjectHandle>> entry in dependencies)
        {
            foreach (ObjectHandle dependency in entry.Value)
            {
                dependents[dependency].Add(entry.Key);
            }
        }
        var remaining = dependencies.ToDictionary(entry => entry.Key, entry => entry.Value.Count);
        var ready = new Queue<ObjectHandle>(remaining.Where(entry => entry.Value == 0).Select(entry => entry.Key));
        while (ready.TryDequeue(out ObjectHandle next))
        {
            remaining.Remove(next);
            foreach (ObjectHandle dependent in dependents[next])
            {
                remaining[dependent]--;
                if (remaining[dependent] == 0)
                {
                    ready.Enqueue(dependent);
                }
            }
        }
        if (remaining.Count == 0)
        {
            return;
        }

        ObjectHandle current = remaining.Keys.First();
        var path = new List<ObjectHandle>();
        var positions = new Dictionary<ObjectHandle, int>();
        while (!positions.ContainsKey(current))
        {
            positions.Add(current, path.Count);
            path.Add(current);
            current = dependencies[current].First(remaining.ContainsKey);
        }
        ObjectIdentifier[] cycle = [.. path.Skip(positions[current]).Append(current).Select(handle => _names[handle])];
        throw new SqlSyntaxException.CyclicTypeDependency(cycle, sources.GetSourceRef(current));

        void AddDependencies(ObjectHandle owner, IEnumerable<DataType> references)
        {
            foreach (DataType reference in references)
            {
                DataType elementType = reference;
                while (elementType is PgDataType.Array array)
                {
                    elementType = array.ElementType;
                }
                if (elementType is PgDataType.Custom custom)
                {
                    dependencies[owner].Add(ObjectHandle.Create(custom.Name, _config.NameHandling));
                }
            }
        }
    }
}
