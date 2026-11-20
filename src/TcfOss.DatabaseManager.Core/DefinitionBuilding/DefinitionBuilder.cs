
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.DefinitionBuilding;

public abstract class DefinitionBuilder<TDefinition, TConfig, TSchemaMapping>(TConfig config, SourceManager sourceManager,
    ILogger logger, ComponentNormalizer componentNormalizer)
    where TConfig : ConfigWithSchemaMapsBase<TSchemaMapping>
    where TSchemaMapping : ISchemaMapping
{
    protected TConfig Config { get; } = config;
    protected QuoteStyle QuoteStyle { get; } = config.QuoteStyle;
    protected SourceManager SourceManager { get; } = sourceManager;
    protected ILogger Logger { get; } = logger;
    protected ComponentNormalizer ComponentNormalizer { get; } = componentNormalizer;
    private readonly NameHandling _nameHandling = config.NameHandling;

    private HashSet<ObjectHandle> ObjectHandles { get; } = [];

    // ReSharper disable MemberCanBeProtected.Global
    // ReSharper disable UnusedMemberInSuper.Global
    protected abstract List<SourcedException> ValidateDefinition(TDefinition definition);

    public abstract TDefinition ToDefinition();

    public abstract void ProcessStatements(IEnumerable<Statement> statements, SchemaIdentifier activeSchemaId, int sourceId);

    protected abstract List<PseudoTable> GetPseudoTables();
    // ReSharper restore MemberCanBeProtected.Global
    // ReSharper restore UnusedMemberInSuper.Global

    protected ObjectNameAndHandle GetName<T>(T statement, SchemaIdentifier activeSchemaId)
        where T : ICreateStatement
    {
        ObjectIdentifier objectId = ComponentNormalizer.NormalizeObjectIdentifier(
            ObjectIdentifier.FromObjectName(statement.Name, activeSchemaId, QuoteStyle));
        ObjectHandle handle = ObjectHandle.Create(statement.Name, activeSchemaId, _nameHandling);

        return new ObjectNameAndHandle(objectId, handle);
    }

    protected ObjectNameAndHandle GetName(Identifier name, SchemaIdentifier activeSchemaId)
    {
        ObjectIdentifier objectId = ComponentNormalizer.NormalizeObjectIdentifier(
            ObjectIdentifier.FromIdentifier(name, activeSchemaId, QuoteStyle));
        ObjectHandle handle = ObjectHandle.Create(objectId, _nameHandling);

        return new ObjectNameAndHandle(objectId, handle);
    }

    protected ObjectNameAndHandle GetAndValidateName<T>(T statement, SchemaIdentifier activeSchemaId, SourceRef? sourceRef)
        where T : ICreateStatement
    {
        ObjectNameAndHandle nameAndHandle = GetName(statement, activeSchemaId);

        if (!ObjectHandles.Add(nameAndHandle.Key))
        {
            throw new SqlSyntaxException.DuplicateObject(nameAndHandle.Id, sourceRef);
        }

        return nameAndHandle;
    }

    protected ObjectNameAndHandle GetAndValidateName(Identifier name, SchemaIdentifier activeSchemaId, SourceRef? sourceRef)
    {
        ObjectNameAndHandle nameAndHandle = GetName(name, activeSchemaId);

        if (!ObjectHandles.Add(nameAndHandle.Key))
        {
            throw new SqlSyntaxException.DuplicateObject(nameAndHandle.Id, sourceRef);
        }

        return nameAndHandle;
    }
}
