using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Postgres.Configuration;

namespace TcfOss.DatabaseManager.Postgres.DefinitionBuilding;

public static class ResolutionExtensions
{
    public static IEnumerable<ObjectHandle> GetCandidateHandles(this PgConfig config, ObjectName name, SchemaIdentifier activeSchema)
    {
        foreach (SchemaIdentifier schema in config.GetCandidateSchemas(name, activeSchema))
        {
            yield return ObjectHandle.Create(name, schema, config.NameHandling);
        }
    }

    public static IEnumerable<SchemaIdentifier> GetCandidateSchemas(this PgConfig config, ObjectName name, SchemaIdentifier activeSchema)
    {
        if (name.Values.Count > 1)
        {
            yield return SchemaIdentifier.FromObjectName(new ObjectName([.. name.Values[..^1]]), activeSchema.Catalog, null);
            yield break;
        }

        foreach (Identifier schema in config.SearchPath)
        {
            yield return new SchemaIdentifier(schema.Name, activeSchema.Catalog, schema.QuoteStyle);
        }
    }
}
