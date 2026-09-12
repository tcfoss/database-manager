using Microsoft.Extensions.DependencyInjection;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Parsing;

namespace TcfOss.DatabaseManager.Core.App;

public static class GenericServiceRegistration
{
    public static void RegisterGenericServices(this IServiceCollection services, ConfigGeneric config)
    {
        services.AddScoped<ConfigGeneric>(_ => config);
        services.AddScoped<ILexer, GenericLexer>();
        services.AddScoped<IParser, Parser>();
        services.AddScoped<IRunCommands, CommandRunner<ConfigGeneric>>();
        services.AddScoped<IReadSchemaMapFiles, DefinitionFileLoader<SchemaMappingBase>>();
        services.AddScoped<IFormatSqlFiles, GenericSqlFormatter>();
        services.AddSingleton<IFunctionNameProvider, FunctionNameProvider>();
    }
}
