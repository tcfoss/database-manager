using Microsoft.EntityFrameworkCore;
using TcfOss.DatabaseManager.MySql.DatabaseComms.EntityFramework;

namespace TcfOss.DatabaseManager.MySql.Tests.DatabaseComms;

public class MyInfoSchemaContextFixture : IDisposable
{
    private readonly DbContextOptions<InfoSchemaContext> _options;

    public InfoSchemaContext Context { get; }

    public MyInfoSchemaContextFixture()
    {
        _options = new DbContextOptionsBuilder<InfoSchemaContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        Context = new InfoSchemaContext(_options);
        Seed(Context);
    }

    public IDbContextFactory<InfoSchemaContext> CreateFactory()
    {
        return new TestInfoSchemaContextFactory(_options);
    }

    public InfoSchemaContext CreateDbContext()
    {
        return new InfoSchemaContext(_options);
    }

    public void Dispose()
    {
        Context.Database.EnsureDeleted();
        Context.Dispose();
        GC.SuppressFinalize(this);
    }

    private static void Seed(InfoSchemaContext context)
    {
        context.Tables.AddRange(MySqlContextTestData.GetTableEntities());
        context.Columns.AddRange(MySqlContextTestData.GetColumnEntities());
        context.TableConstraints.AddRange(MySqlContextTestData.GetTableConstraintEntities());
        context.ReferentialConstraints.AddRange(MySqlContextTestData.GetReferentialConstraintEntities());
        context.KeyColumnUsages.AddRange(MySqlContextTestData.GetKeyColumnUsageEntities());
        context.Statistics.AddRange(MySqlContextTestData.GetStatisticsEntities());
        context.CheckConstraints.AddRange(MySqlContextTestData.GetCheckConstraintEntities());
        context.Routines.AddRange(MySqlContextTestData.GetRoutineEntities());
        context.Parameters.AddRange(MySqlContextTestData.GetParameterEntities());
        context.Triggers.AddRange(MySqlContextTestData.GetTriggerEntities());
        context.Views.AddRange(MySqlContextTestData.GetViewEntities());
        context.Events.AddRange(MySqlContextTestData.GetEventEntities());
        context.Schematas.AddRange(MySqlContextTestData.GetSchemataEntities());
        context.Engines.AddRange(MySqlContextTestData.GetEngineEntities());
        context.CharacterSets.AddRange(MySqlContextTestData.GetCharacterSetEntities());
        context.CollationCharacterSetApplicabilities.AddRange(MySqlContextTestData.GetCollationCharacterSetApplicabilityEntities());

        context.SaveChanges();
    }

    private sealed class TestInfoSchemaContextFactory(DbContextOptions<InfoSchemaContext> options) : IDbContextFactory<InfoSchemaContext>
    {
        private readonly DbContextOptions<InfoSchemaContext> _options = options;

        public InfoSchemaContext CreateDbContext()
        {
            return new InfoSchemaContext(_options);
        }

        public ValueTask<InfoSchemaContext> CreateDbContextAsync()
        {
            return ValueTask.FromResult(CreateDbContext());
        }
    }
}
