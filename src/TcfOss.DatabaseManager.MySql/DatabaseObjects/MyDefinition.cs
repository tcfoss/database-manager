using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.MySql.DatabaseObjects;

public class MyDefinition : IHaveObjects
{
    public DatabaseObjectDict<MyTable> Tables { get; init; } = [];
    public DatabaseObjectDict<MyStoredProcedure> Procedures { get; init; } = [];
    public DatabaseObjectDict<MyStoredFunction> Functions { get; init; } = [];
    public DatabaseObjectDict<MyTrigger> Triggers { get; init; } = [];
    public DatabaseObjectDict<MyView> Views { get; init; } = [];
    public DatabaseObjectDict<MyEvent> Events { get; init; } = [];

    public MyDefinition Copy()
    {
        return new MyDefinition
        {
            Tables = Tables.Clone(),
            Procedures = Procedures.Clone(),
            Functions = Functions.Clone(),
            Triggers = Triggers.Clone(),
            Views = Views.Clone(),
            Events = Events.Clone()
        };
    }

    public ObjectType? GetObjectType(ObjectHandle handle)
    {
        if (Tables.ContainsKey(handle))
        {
            return ObjectType.Table;
        }
        else if (Procedures.ContainsKey(handle))
        {
            return ObjectType.Procedure;
        }
        else if (Functions.ContainsKey(handle))
        {
            return ObjectType.Function;
        }
        else if (Triggers.ContainsKey(handle))
        {
            return ObjectType.Trigger;
        }
        else if (Views.ContainsKey(handle))
        {
            return ObjectType.View;
        }
        else if (Events.ContainsKey(handle))
        {
            return ObjectType.Event;
        }
        else
        {
            return null;
        }
    }

    public ObjectType? GetObjectType(SqlValueList<Identifier> name, SchemaIdentifier activeSchema, NameHandling nameHandling)
    {
        ObjectHandle handle = ObjectHandle.Create(name, activeSchema, nameHandling);
        return GetObjectType(handle);
    }

    public PseudoTableSet ToPseudoTableSet(SchemaIdentifier? schemaId)
    {
        var sources = new List<PseudoTable>();
        foreach (MyTable table in Tables.Values)
        {
            sources.Add(table.ToPseudoTable());
        }
        foreach (MyView view in Views.Values)
        {
            sources.Add(view.ToPseudoTable());
        }

        return new PseudoTableSet(schemaId, sources);
    }
}
