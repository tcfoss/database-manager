
using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.DefinitionBuilding;

public abstract class DefinitionBuilder<TDefinition, TConfig, TSchemaMapping>(TConfig config, SourceManager sourceManager, ILogger logger)
    where TConfig : ConfigWithSchemaMapsBase<TSchemaMapping>
    where TSchemaMapping : ISchemaMapping
{
    protected TConfig Config { get; } = config;
    protected QuoteStyle QuoteStyle { get; } = config.QuoteStyle;
    protected SourceManager SourceManager { get; } = sourceManager;
    protected ILogger Logger { get; } = logger;
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

    protected ObjectNameAndHandle GetAndValidateName<T>(T statement, ObjectIdentifier? objectId, SchemaIdentifier activeSchemaId, SourceRef? sourceRef) where T : ICreateStatement
    {
        objectId ??= ObjectIdentifier.FromObjectName(statement.Name, activeSchemaId, QuoteStyle);

        ObjectHandle handle = ObjectHandle.Create(objectId, _nameHandling);

        if (!ObjectHandles.Add(handle))
        {
            throw new SqlSyntaxException.DuplicateObject(objectId, sourceRef);
        }

        return new ObjectNameAndHandle(objectId, handle);
    }
}
