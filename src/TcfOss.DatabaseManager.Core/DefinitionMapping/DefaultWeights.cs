namespace TcfOss.DatabaseManager.Core.DefinitionMapping;

public struct DefaultWeights
{
    public const uint PreDeploymentScript = 1;
    public const uint InsertPreDeployMeta = 9_999;

    public const uint DropProgramObject = 10_000;
    public const uint DropView = 10_010;

    public const uint ApplyRename = 11_000;
    public const uint InsertRefactor = 20_000;

    public const uint DropForeignKey = 30_000;
    public const uint DropAutoIncrement = 30_010;
    public const uint DropPrimaryKey = 30_020;
    public const uint DropKey = 30_030;

    public const uint PostDropConstraintsScript = 40_000;
    public const uint InsertPostDropConstraintsMeta = 49_999;

    public const uint DropTable = 50_000;

    public const uint SetCharacterSetCollation = 50_950;

    public const uint AlterTable = 51_000;

    public const uint PreSetNotNullScript = 67_800;
    public const uint InsertPreSetNotNullMeta = 67_850;
    public const uint SetNotNull = 67_900;
    public const uint CreateTable = 68_000;

    public const uint PreAddConstraintsScript = 70_000;
    public const uint InsertPreAddConstraintsMeta = 79_999;

    public const uint AddKey = 80_000;
    public const uint AddPrimaryKey = 80_010;
    public const uint AddAutoIncrement = 80_020;
    public const uint AddForeignKey = 80_030;

    public const uint CreateProgramObject = 90_010;
    public const uint CreateTrigger = 95_000;
    public const uint CreateEvent = 95_010;

    public const uint PostDeploymentScript = 100_000;
    public const uint InsertPostDeployMeta = 109_999;
}
