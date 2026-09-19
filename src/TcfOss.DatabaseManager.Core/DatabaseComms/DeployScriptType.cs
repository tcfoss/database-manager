namespace TcfOss.DatabaseManager.Core.DatabaseComms;

public enum DeployScriptType
{
    // Leave room for PreBuild = 1 (TODO)
    PreDeployment = 20,
    PostDropConstraints = 30,
    PreSetNotNull = 40,
    PreAddConstraints = 50,
    PostDeployment = 60,
}
