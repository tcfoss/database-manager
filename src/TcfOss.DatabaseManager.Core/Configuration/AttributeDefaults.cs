using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;

namespace TcfOss.DatabaseManager.Core.Configuration;

public class AttributeDefaults
{
    public MySqlNumericAttribute? NumericAttribute { get; init; } = MySqlNumericAttribute.Signed;

    public uint? SignedIntWidth { get; init; } = 11;
    public uint? UnsignedIntWidth { get; init; } = 10;
    public uint? SignedBigIntWidth { get; init; } = 20;
    public uint? UnsignedBigIntWidth { get; init; } = 20;
    public uint? SignedMediumIntWidth { get; init; } = 9;
    public uint? UnsignedMediumIntWidth { get; init; } = 8;
    public uint? SignedSmallIntWidth { get; init; } = 6;
    public uint? UnsignedSmallIntWidth { get; init; } = 5;
    public uint? SignedTinyIntWidth { get; init; } = 4;
    public uint? UnsignedTinyIntWidth { get; init; } = 3;

    public NumericLength? DecimalPrecision { get; init; } = new NumericLength.PrecisionScale(10, 0);

    public NumericLength? FloatPrecision { get; init; }
    public NumericLength? DoublePrecision { get; init; }

    public uint? YearPrecision { get; init; } = 4;

    public ReferentialAction? ForeignKeyOnUpdate { get; init; } = ReferentialAction.Restrict;
    public ReferentialAction? ForeignKeyOnDelete { get; init; } = ReferentialAction.Restrict;

    public IndexMethod IndexMethod { get; init; } = IndexMethod.Btree;

    public SqlDataRelation? FunctionDataRelation { get; init; } = SqlDataRelation.ContainsSql;
    public SqlDataRelation? ProcedureDataRelation { get; init; } = SqlDataRelation.ContainsSql;

    public SecurityContext FunctionSecurityContext { get; init; } = SecurityContext.Definer;
    public SecurityContext ProcedureSecurityContext { get; init; } = SecurityContext.Definer;
    public SecurityContext ViewSecurityContext { get; init; } = SecurityContext.Definer;

    public RoutineParameterDirection? FunctionParameterDirection { get; init; } = RoutineParameterDirection.In;
    public RoutineParameterDirection? ProcedureParameterDirection { get; init; } = RoutineParameterDirection.In;

    public EventEnabledStatus EventEnabledStatus { get; init; } = EventEnabledStatus.Enable;
}
