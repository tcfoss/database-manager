using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.MsSql.BuiltIn;
using TcfOss.DatabaseManager.MsSql.Configuration;
using TcfOss.DatabaseManager.MsSql.DatabaseComms.EntityFramework;
using TcfOss.DatabaseManager.MsSql.Lexing;
using TcfOss.DatabaseManager.MsSql.Parsing;

namespace TcfOss.DatabaseManager.MsSql.App;

public static class MsSqlServiceRegistration
{
    public static void RegisterMsSqlServices(this IServiceCollection services, MsConfig config)
    {
        services.AddScoped<MsConfig>(_ => config);
        services.AddScoped<ConfigWithSchemaMapsBase<MsSchemaMapping>>(_ => config);
        services.AddScoped<ILexer, MsLexer>();
        services.AddScoped<IParser, MsParser>();
        services.AddScoped<IParseText, TextParser>();
        services.AddPooledDbContextFactory<SysContext>((_, options) =>
        {
            options.UseSqlServer(
                config.ConnectionString,
                sqlServerOpts =>
                {
                    sqlServerOpts.EnableRetryOnFailure();
                });
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            options.EnableDetailedErrors();
            if (config.Logging.EnableSensitiveDataLogging)
            {
                options.EnableSensitiveDataLogging();
            }
        });
        services.AddScoped<SysContext>(sp => sp.GetRequiredService<IDbContextFactory<SysContext>>().CreateDbContext());
        services.AddSingleton<IFunctionNameProvider, MsFunctionNameProvider>();
        // TODO: services.AddScoped<IRetrieveDatabaseObjects, MsInfoSchemaRepo>()
        // TODO: services.AddScoped<IRetrieveRawEntities, MsRawEntityRetriever>()
        // TODO: services.AddScoped<ILoadFsDefinition<MsDefinition>, MsFsDefinitionLoader>()
        // TODO: services.AddScoped<ILoadDbDefinition<MsDefinition>, MsDbDefinitionLoader>()
        // TODO: services.AddScoped<IGetUnappliedRefactors, MsDbDefinitionLoader>()
        // TODO: services.AddScoped<IGetUnappliedDeployScripts, MsDbDefinitionLoader>()
        // TODO: services.AddScoped<IDownloadSchema, MsDownloadSchema>()
        // TODO: services.AddScoped<IComputeChanges, MsChangeComputer>()
        services.AddScoped<IRunCommands, MsCommandRunner>();
        // TODO: services.AddScoped<IReadSchemaMapFiles, DefinitionFileLoader<MsSchemaMapping>>()
        services.AddScoped<IFormatSqlFiles, MsSqlFormatter>();
    }
}
