using TcfOss.DatabaseManager.Core.Configuration.Parsing.Attributes;

namespace TcfOss.DatabaseManager.Core.Configuration.Parsing;

// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

public class AttributeDefaults
{
    public MySqlNumericAttribute NumericAttribute { get; set; } = MySqlNumericAttribute.NotSet;
    public int? SignedIntWidth { get; set; } = -1;
    public int? UnsignedIntWidth { get; set; } = -1;
    public int? SignedBigIntWidth { get; set; } = -1;
    public int? UnsignedBigIntWidth { get; set; } = -1;
    public int? SignedMediumIntWidth { get; set; } = -1;
    public int? UnsignedMediumIntWidth { get; set; } = -1;
    public int? SignedSmallIntWidth { get; set; } = -1;
    public int? UnsignedSmallIntWidth { get; set; } = -1;
    public int? SignedTinyIntWidth { get; set; } = -1;
    public int? UnsignedTinyIntWidth { get; set; } = -1;
    public int? DecimalPrecision { get; set; } = -1;
    public int? DecimalScale { get; set; } = -1;
    public int? FloatPrecision { get; set; } = -1;
    public int? FloatScale { get; set; } = -1;
    public int? DoublePrecision { get; set; } = -1;
    public int? DoubleScale { get; set; } = -1;
    public int? YearPrecision { get; set; } = -1;
    public IndexMethod IndexMethod { get; set; } = IndexMethod.NotSet;
    public ReferentialAction ForeignKeyOnUpdate { get; set; } = ReferentialAction.NotSet;
    public ReferentialAction ForeignKeyOnDelete { get; set; } = ReferentialAction.NotSet;
    public SqlDataRelation? FunctionDataRelation { get; set; } = SqlDataRelation.NotSet;
    public SqlDataRelation? ProcedureDataRelation { get; set; } = SqlDataRelation.NotSet;
    public SecurityContext? FunctionSecurityContext { get; set; } = SecurityContext.NotSet;
    public SecurityContext? ProcedureSecurityContext { get; set; } = SecurityContext.NotSet;
    public SecurityContext? ViewSecurityContext { get; set; } = SecurityContext.NotSet;
    public RoutineParameterDirection? FunctionParameterDirection { get; set; } = RoutineParameterDirection.NotSet;
    public RoutineParameterDirection? ProcedureParameterDirection { get; set; } = RoutineParameterDirection.NotSet;
    public DatabaseObjects.Attributes.EventEnabledStatus? EventEnabledStatus { get; set; }
}
