using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.MySql.Configuration;

namespace TcfOss.DatabaseManager.MariaDb.Configuration;

public class MaConfigLoader(ILogger logger) : MyConfigLoader(logger)
{
    public override Version DefaultVersion { get; } = new(11, 8, 2);

    protected override AttributeDefaults GetAttributeDefaults()
    {
        return new AttributeDefaults
        {
            NumericAttribute = MySqlNumericAttribute.Signed,
            SignedIntWidth = 11,
            UnsignedIntWidth = 10,
            SignedBigIntWidth = 20,
            UnsignedBigIntWidth = 20,
            SignedMediumIntWidth = 9,
            UnsignedMediumIntWidth = 8,
            SignedSmallIntWidth = 6,
            UnsignedSmallIntWidth = 5,
            SignedTinyIntWidth = 4,
            UnsignedTinyIntWidth = 3,
            DecimalPrecision = new NumericLength.PrecisionScale(10, 0),
            FloatPrecision = null,
            DoublePrecision = null,
            YearPrecision = 4,
            ForeignKeyOnDelete = ReferentialAction.Restrict,
            ForeignKeyOnUpdate = ReferentialAction.Restrict,
            FunctionDataRelation = SqlDataRelation.ContainsSql,
            ProcedureDataRelation = SqlDataRelation.ContainsSql,
            FunctionParameterDirection = RoutineParameterDirection.In,
            ProcedureParameterDirection = RoutineParameterDirection.In,
        };
    }

    protected override NormalizationSettings GetViewNormalizationDefaults()
    {
        return new NormalizationSettings
        {
            IdentifierQuotationHandling = IdentifierQuotationHandling.Always,
            CteDeclarationNameQuotationHandling = IdentifierQuotationHandling.OnlyIfSpecial,
            AccountQuoteStyle = ExtendedQuoteStyle.Backticks,
            CastConvertVarcharToChar = true,
            CastAddCharsetToType = true,
        };
    }

    protected override ValidationSettings GetValidationSettings()
    {
        return new ValidationSettings()
        {
            AllowCheckOnColumn = true,
            AllowNamedColumnDefault = false,
            AllowNamedColumnUnique = false,
            AllowNamedColumnNullability = false,
            AllowNamedColumnCheck = false,
        };
    }
}
