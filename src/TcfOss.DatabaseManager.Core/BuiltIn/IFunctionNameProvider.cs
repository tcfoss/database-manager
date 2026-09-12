namespace TcfOss.DatabaseManager.Core.BuiltIn;

public interface IFunctionNameProvider
{
    public bool IsBuiltInFunction(string name);
    public bool IsReservedKeyword(string name);
}
