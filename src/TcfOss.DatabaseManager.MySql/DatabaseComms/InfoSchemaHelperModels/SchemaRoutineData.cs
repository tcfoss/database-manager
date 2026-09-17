namespace TcfOss.DatabaseManager.MySql.DatabaseComms.InfoSchemaHelperModels;

public sealed record SchemaRoutineData(
    IReadOnlyDictionary<(string Name, string Type), RoutineDto> RoutinesByName,
    IReadOnlyDictionary<(string Name, string Type), IReadOnlyList<RoutineParameterDto>> ParametersByRoutine);
