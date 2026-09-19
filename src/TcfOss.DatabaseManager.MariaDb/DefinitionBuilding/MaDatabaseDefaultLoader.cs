using Microsoft.EntityFrameworkCore;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.MariaDb.DatabaseComms.EntityFramework;
using TcfOss.DatabaseManager.MySql.Configuration;
using TcfOss.DatabaseManager.MySql.DatabaseComms.EntityFramework.PseudoEntities;

namespace TcfOss.DatabaseManager.MariaDb.DefinitionBuilding;

public class MaDatabaseDefaultLoader(InfoSchemaContext context) : ILoadDatabaseDefaults
{
    private readonly InfoSchemaContext _context = context;

    public SchemaDefaults GetServerDefaults(SqlDialect dialect, Version version)
    {
        ServerVariable engine = _context
            .Database
            .SqlQueryRaw<ServerVariable>("SHOW VARIABLES LIKE 'default_storage_engine';")
            .ToList().First();
        ServerVariable characterSet = _context
            .Database
            .SqlQueryRaw<ServerVariable>("SHOW VARIABLES LIKE 'character_set_server';")
            .ToList().First();
        ServerVariable collation = _context
            .Database
            .SqlQueryRaw<ServerVariable>("SHOW VARIABLES LIKE 'collation_server';")
            .ToList().First();
        return new SchemaDefaults
        {
            Engine = engine.Value!,
            CharacterSet = characterSet.Value!,
            Collation = collation.Value!,
        };
    }

    private SchemaDefaults? ReadSchemaDefaultsFromDatabase(string schemaName, string catalogName, string defaultEngine)
    {
        var stringQuery = (from s in _context.Schematas
                           where schemaName == s.SchemaName
                           && s.CatalogName == catalogName
                           select new
                           {
                               s.SchemaName,
                               s.DefaultCharacterSetName,
                               s.DefaultCollationName
                           }).FirstOrDefault();

        if (stringQuery == null)
        {
            return null;
        }

        return new SchemaDefaults
        {
            CharacterSet = stringQuery.DefaultCharacterSetName,
            Collation = stringQuery.DefaultCollationName,
            Engine = defaultEngine
        };
    }

    public SchemaDefaults GetSchemaDefaults(string schemaName, string catalogName, SchemaDefaults serverDefaults)
    {
        SchemaDefaults? defaultsFromDatabase = ReadSchemaDefaultsFromDatabase(schemaName, catalogName, serverDefaults.Engine);
        if (defaultsFromDatabase != null)
        {
            return defaultsFromDatabase;
        }
        return serverDefaults;
    }

    public Dictionary<string, CharacterSetSpec> GetCharacterSets(SqlDialect dialect, Version version)
    {
        var characterSets = (from cs in _context.CharacterSets
                             join apl in _context.CollationCharacterSetApplicabilities
                             on cs.CharacterSetName equals apl.CharacterSetName
                             select new
                             {
                                 cs.CharacterSetName,
                                 apl.FullCollationName,
                                 apl.IsDefault
                             }).GroupBy(x => x.CharacterSetName);

        var characterSetSpecs = new Dictionary<string, CharacterSetSpec>();
        foreach (var group in characterSets)
        {
            string characterSetName = group.Key;
            var collations = group.Select(x => x.FullCollationName).ToHashSet();
            string defaultCollation = group.First(x => x.IsDefault == "Yes").FullCollationName;

            characterSetSpecs[characterSetName] = new CharacterSetSpec
            {
                CharacterSet = characterSetName,
                DefaultCollation = defaultCollation,
                Collations = collations
            };
        }
        return characterSetSpecs;
    }
}
