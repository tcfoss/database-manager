using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.DefinitionBuilding;

/// <summary>
/// Manages a set of pseudo-tables for definition building and query analysis.
///
/// An "external" source is one that **is available** to be added to a FROM clause. The primary
/// examples are real tables and views. Within the context of a specific query, external
/// sources also include CTEs and derived tables (subqueries in the FROM clause).
///
/// In the context of a script or stored program, external sources can also include temporary
/// tables and table variables (for dialects that support them).
///
/// A "local" source is one that is **already referenced** in a FROM clause (or a
/// JOIN) and is therefore available to be SELECTed.
///
/// **WARNING**: This object is mutable. It is intended to be built up as a query is analyzed
/// or normalized. Before passing it into other contexts, take due consideration of whether
/// it needs to be cloned.
/// </summary>
public class PseudoTableSet
{
    private SchemaIdentifier? ActiveSchema { get; }
    private readonly List<PseudoTable> _externalSources;
    private readonly List<List<PseudoTable>> _localSources;

    public PseudoTableSet(SchemaIdentifier? activeSchema, IEnumerable<PseudoTable> externalSources, IEnumerable<IEnumerable<PseudoTable>>? localSources = null)
    {
        ActiveSchema = activeSchema;
        _externalSources = [.. externalSources];
        _localSources = [];
        if (localSources != null)
        {
            foreach (IEnumerable<PseudoTable> sourceList in localSources)
            {
                _localSources.Add([.. sourceList]);
            }
        }
    }

    public PseudoTable GetRequiredPseudoTable(ObjectName tableName, SourceRef? sourceRef = null)
    {
        return tableName.Values.Count switch
        {
            1 => GetRequiredPseudoTable(tableName.Values[0].Name, null, null, sourceRef),
            2 => GetRequiredPseudoTable(tableName.Values[1].Name, tableName.Values[0].Name, null, sourceRef),
            3 => GetRequiredPseudoTable(tableName.Values[2].Name, tableName.Values[1].Name, tableName.Values[0].Name, sourceRef),
            _ => throw new IdentifierMismatchException.IdentifierLengthException("Table", 3, tableName.Values.Count, tableName.Values)
        };
    }

    private PseudoTable GetRequiredPseudoTable(string tableName, string? schemaName = null, string? catalogName = null, SourceRef? sourceRef = null)
    {
        PseudoTable[] matches = GetPseudoTables(tableName, schemaName, catalogName);

        int num = matches.Length;
        if (num == 0)
        {
            throw new SqlSyntaxException.SelectSourceNotFound([catalogName, schemaName, tableName], sourceRef);
        }
        if (num > 1)
        {
            if (schemaName == null)
            {
                PseudoTable[] disambiguation = [.. matches.Where(x => x.Identifier!.Schema.Name == "")];
                if (disambiguation.Length == 1)
                {
                    return disambiguation[0];
                }
                if (ActiveSchema != null)
                {
                    disambiguation = [.. matches.Where(x => x.Identifier!.Schema.Name == ActiveSchema)];
                    if (disambiguation.Length == 1)
                    {
                        return disambiguation[0];
                    }
                }
            }
            throw new SqlSyntaxException.ViewNonUniqueReferenceName([catalogName, schemaName, tableName], sourceRef);
        }
        return matches[0];
    }

    public PseudoTable[] GetPseudoTables(ObjectName objectName)
    {
        return GetPseudoTables(
            tableName: objectName.Values.Last().Name,
            schemaName: objectName.Values.Count >= 2 ? objectName.Values[^2].Name : null,
            catalogName: objectName.Values.Count == 3 ? objectName.Values[0].Name : null);
    }

    public PseudoTable[] GetPseudoTables(CompoundIdentifier identifier)
    {
        return GetPseudoTables(
            tableName: identifier.Identifiers.Last().Name,
            schemaName: identifier.Identifiers.Count >= 2 ? identifier.Identifiers[^2].Name : null,
            catalogName: identifier.Identifiers.Count == 3 ? identifier.Identifiers[0].Name : null);
    }


    public PseudoTable[] GetPseudoTables(string tableName, string? schemaName = null, string? catalogName = null)
    {
        IEnumerable<PseudoTable> matches = _externalSources.Where(x => x.Identifier!.Name == tableName);
        if (schemaName != null)
        {
            matches = matches.Where(x => x.Identifier!.Schema.Name == schemaName);
        }
        if (catalogName != null)
        {
            matches = matches.Where(x => x.Identifier!.Schema.Catalog.Name == catalogName);
        }

        return [.. matches];
    }

    public SelectableItem GetRequiredSelectableElement(CompoundIdentifier identifier, SourceRef? sourceRef = null)
    {
        return identifier.Identifiers.Count switch
        {
            1 => GetRequiredSelectableElement(identifier.Identifiers[0].Name, sourceName: null, sourceParentName: null, sourceRef: sourceRef),
            2 => GetRequiredSelectableElement(identifier.Identifiers[1].Name, sourceName: identifier.Identifiers[0].Name, sourceParentName: null, sourceRef: sourceRef),
            3 => GetRequiredSelectableElement(identifier.Identifiers[2].Name, sourceName: identifier.Identifiers[1].Name, sourceParentName: identifier.Identifiers[0].Name, sourceRef: sourceRef),
            _ => throw new IdentifierMismatchException.IdentifierLengthException("SELECT item", 2, identifier.Identifiers.Count, identifier.Identifiers)
        };
    }

    public SelectableItem GetRequiredSelectableElement(string name, string? sourceName = null, string? sourceParentName = null, SourceRef? sourceRef = null)
    {
        SelectableItem[] matches = GetSelectableElements(name, sourceName, sourceParentName);

        if (matches.Length == 0)
        {
            throw new SqlSyntaxException.SelectIdentifierNotFound([sourceName, name], sourceRef);
        }
        if (matches.Length > 1)
        {
            throw new SqlSyntaxException.SelectIdentifierNotUnique([sourceName, name], sourceRef);
        }
        return matches[0];
    }

    private SelectableItem[] GetSelectableItems(int depth, string name, string? sourceName = null, string? sourceParentName = null)
    {
        List<PseudoTable> currentSources = _localSources[^(depth + 1)];
        IEnumerable<PseudoTable> matches = currentSources.Where(x => x.SelectableItems.Contains(name));
        if (sourceName != null)
        {
            matches = matches.Where(x => x.Name == sourceName);
        }
        if (sourceParentName != null)
        {
            matches = matches.Where(x => x.Identifier!.Schema.Name == sourceParentName);
        }

        return [.. matches.Select(x =>
        {
            if (x.Alias != null)
            {
                return new SelectableItem(name, x.Alias) { SourceType = x.Type, SourceIdentifier = x.Identifier };
            }
            if (x.Identifier != null)
            {
                return new SelectableItem(name, x.Identifier.Name, x.Identifier.Schema.Name) { SourceType = x.Type, SourceIdentifier = x.Identifier };
            }
            return new SelectableItem(name, x.Name) { SourceType = x.Type, SourceIdentifier = x.Identifier };
        })];
    }

    public SelectableItem[] GetSelectableElements(string name, string? sourceName = null, string? sourceParentName = null)
    {
        int depth = 0;
        while (depth < _localSources.Count)
        {
            SelectableItem[] items = GetSelectableItems(depth, name, sourceName, sourceParentName);
            if (items.Length > 0)
            {
                return items;
            }
            depth++;
        }
        return [];
    }

    public void AddExternalSource(string name, PseudoTable newSource)
    {
        _externalSources.Add(newSource with { Name = name, Identifier = new ObjectIdentifier(name, new SchemaIdentifier("", new CatalogIdentifier(""))) });
    }

    public void AddLocalSource(string name, PseudoTable newSource, string? alias = null)
    {
        _localSources[^1].Add(newSource with { Name = name, Alias = alias });
    }

    public void EnterSelectScope()
    {
        _localSources.Add([]);
    }

    public void LeaveSelectScope()
    {
        if (_localSources.Count == 0)
        {
            throw new InvalidOperationException("Cannot leave SELECT scope when none exists.");
        }
        _localSources.RemoveAt(_localSources.Count - 1);
    }

    public PseudoTableSet Clone()
    {
        return new PseudoTableSet(ActiveSchema, [.. _externalSources], [.. _localSources]);
    }

    public PseudoTableSet CloneExternalOnly()
    {
        return new PseudoTableSet(ActiveSchema, [.. _externalSources]);
    }

    /// <summary>
    /// Returns all selectable columns from the innermost local scope, optionally
    /// filtered to a specific source (table name or alias). Returns an empty list
    /// if there is no local scope, the scope is empty, or no matching sources are found.
    /// </summary>
    /// <param name="sourceName">If provided, only columns from this source are returned.</param>
    public IReadOnlyList<SelectableItem> GetLocalWildcardColumns(string? sourceName = null)
    {
        if (_localSources.Count == 0)
        {
            return [];
        }

        IEnumerable<PseudoTable> sources = _localSources[^1];
        if (sourceName != null)
        {
            sources = sources.Where(x => x.Name == sourceName);
        }

        var result = new List<SelectableItem>();
        foreach (PseudoTable table in sources)
        {
            string displayName = table.Alias ?? table.Name;
            foreach (string column in table.SelectableItems)
            {
                result.Add(new SelectableItem(column, displayName));
            }
        }
        return result;
    }

    public readonly record struct SelectableItem(string Name, string Source, string? Schema = null)
    {
        public PseudoTableType SourceType { get; init; }
        public ObjectIdentifier? SourceIdentifier { get; init; }

        public string[] ToArray()
        {
            if (!string.IsNullOrEmpty(Schema))
            {
                return [Schema, Source, Name];
            }
            return [Source, Name];
        }
    }
}
