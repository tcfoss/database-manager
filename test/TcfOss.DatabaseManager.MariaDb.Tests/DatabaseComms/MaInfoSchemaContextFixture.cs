using Microsoft.EntityFrameworkCore;
using InfoSchemaContext = TcfOss.DatabaseManager.MariaDb.DatabaseComms.EntityFramework.InfoSchemaContext;

namespace TcfOss.DatabaseManager.MariaDb.Tests.DatabaseComms;

// ReSharper disable once ClassNeverInstantiated.Global
public class MaInfoSchemaContextFixture : IDisposable
{
    private readonly DbContextOptions<InfoSchemaContext> _options;

    public InfoSchemaContext Context { get; }

    public MaInfoSchemaContextFixture()
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
        context.Tables.AddRange(MariaDbContextTestData.GetTableEntities());
        context.Columns.AddRange(MariaDbContextTestData.GetColumnEntities());
        context.TableConstraints.AddRange(MariaDbContextTestData.GetTableConstraintEntities());
        context.ReferentialConstraints.AddRange(MariaDbContextTestData.GetReferentialConstraintEntities());
        context.KeyColumnUsages.AddRange(MariaDbContextTestData.GetKeyColumnUsageEntities());
        context.Statistics.AddRange(MariaDbContextTestData.GetStatisticsEntities());
        context.CheckConstraints.AddRange(MariaDbContextTestData.GetCheckConstraintEntities());
        context.Routines.AddRange(MariaDbContextTestData.GetRoutineEntities());
        context.Parameters.AddRange(MariaDbContextTestData.GetParameterEntities());
        context.Triggers.AddRange(MariaDbContextTestData.GetTriggerEntities());
        context.Views.AddRange(MariaDbContextTestData.GetViewEntities());
        context.Events.AddRange(MariaDbContextTestData.GetEventEntities());
        context.Schematas.AddRange(MariaDbContextTestData.GetSchemataEntities());
        context.Engines.AddRange(MariaDbContextTestData.GetEngineEntities());
        context.CharacterSets.AddRange(MariaDbContextTestData.GetCharacterSetEntities());
        context.CollationCharacterSetApplicabilities.AddRange(MariaDbContextTestData.GetCollationCharacterSetApplicabilityEntities());
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
