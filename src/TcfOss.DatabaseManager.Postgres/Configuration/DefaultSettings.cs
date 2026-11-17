using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;

namespace TcfOss.DatabaseManager.Postgres.Configuration;

public static class DefaultSettings
{
    public static AttributeDefaults GetAttributeDefaults()
    {
        return new AttributeDefaults
        {
            NumericAttribute = null,
            SignedIntWidth = null,
            UnsignedIntWidth = null,
            SignedBigIntWidth = null,
            UnsignedBigIntWidth = null,
            SignedMediumIntWidth = null,
            UnsignedMediumIntWidth = null,
            SignedSmallIntWidth = null,
            UnsignedSmallIntWidth = null,
            SignedTinyIntWidth = null,
            UnsignedTinyIntWidth = null,
            DecimalPrecision = null,
            FloatPrecision = null,
            DoublePrecision = null,
            YearPrecision = null,
            ForeignKeyOnUpdate = ReferentialAction.NoAction,
            ForeignKeyOnDelete = ReferentialAction.NoAction,
            FunctionDataRelation = null,
            ProcedureDataRelation = null,
            FunctionSecurityContext = SecurityContext.Invoker,
            ProcedureSecurityContext = SecurityContext.Invoker,
            ViewSecurityContext = SecurityContext.Definer,
            FunctionParameterDirection = RoutineParameterDirection.In,
            ProcedureParameterDirection = RoutineParameterDirection.In
        };
    }

    public static NormalizationSettings GetNormalizationDefaults()
    {
        return new NormalizationSettings
        {
            IdentifierQuotationHandling = IdentifierQuotationHandling.Always,
            CteDeclarationNameQuotationHandling = IdentifierQuotationHandling.Always
        };
    }
}
