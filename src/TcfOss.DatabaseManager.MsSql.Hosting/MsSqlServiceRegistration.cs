using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TcfOss.DatabaseManager.Core.App;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Configuration;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.MsSql.App;
using TcfOss.DatabaseManager.MsSql.BuiltIn;
using TcfOss.DatabaseManager.MsSql.Configuration;
using TcfOss.DatabaseManager.MsSql.DatabaseComms.EntityFramework;
using TcfOss.DatabaseManager.MsSql.Lexing;
using TcfOss.DatabaseManager.MsSql.Parsing;

namespace TcfOss.DatabaseManager.MsSql.Hosting;

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
        /* TODO: Implement Services */
        // services.AddScoped<IRetrieveDatabaseObjects, MsInfoSchemaRepo>()
        // services.AddScoped<IRetrieveRawEntities, MsRawEntityRetriever>()
        // services.AddScoped<ILoadFsDefinition<MsDefinition>, MsFsDefinitionLoader>()
        // services.AddScoped<ILoadDbDefinition<MsDefinition>, MsDbDefinitionLoader>()
        // services.AddScoped<IGetUnappliedRefactors, MsDbDefinitionLoader>()
        // services.AddScoped<IGetUnappliedDeployScripts, MsDbDefinitionLoader>()
        // services.AddScoped<IDownloadSchema, MsDownloadSchema>()
        // services.AddScoped<IComputeChanges, MsChangeComputer>()
        services.AddScoped<IRunCommands, MsCommandRunner>();
        // services.AddScoped<IReadSchemaMapFiles, DefinitionFileLoader<MsSchemaMapping>>()
        services.AddScoped<IFormatSqlFiles, MsSqlFormatter>();
    }
}
