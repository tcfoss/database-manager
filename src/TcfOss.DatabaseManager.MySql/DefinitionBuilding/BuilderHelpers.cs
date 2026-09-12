namespace TcfOss.DatabaseManager.MySql.DefinitionBuilding;

public class BuilderHelpers
{
    public required MyConstructProcedure ProcedureConstructor { get; init; }
    public required MyConstructFunction FunctionConstructor { get; init; }
    public required MyConstructPreTrigger TriggerConstructor { get; init; }
    public required MyConstructPreView ViewConstructor { get; init; }
    public required MyConstructEvent EventConstructor { get; init; }
}
