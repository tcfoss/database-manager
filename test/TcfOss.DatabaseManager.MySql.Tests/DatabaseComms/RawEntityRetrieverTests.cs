using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.MySql.DatabaseComms;

namespace TcfOss.DatabaseManager.MySql.Tests.DatabaseComms;

public class RawEntityRetrieverTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Batch_ReadsDefinitionsAndPreservesSchemaIdentity(bool triggers)
    {
        var connection = new FakeConnection();
        string nameColumn = triggers ? "Trigger" : "Function";
        string createColumn = triggers ? "SQL Original Statement" : "Create Function";
        connection.Results.Tables.Add(Result(nameColumn, createColumn, "same`name", "first definition"));
        connection.Results.Tables.Add(Result(nameColumn, createColumn, "same`name", "second definition"));
        using var context = CreateContext(connection);
        using var retriever = new RawEntityRetriever(context);
        ObjectIdentifier first = ObjectIdentifier.FromStrings("def", "first`schema", "same`name");
        ObjectIdentifier second = ObjectIdentifier.FromStrings("def", "second", "same`name");

        if (triggers)
        {
            var results = await retriever.GetCreateTriggersAsync([first, second], TestContext.Current.CancellationToken);
            Assert.Equal("first definition", results[first].CreateTrigger);
            Assert.Equal("second definition", results[second].CreateTrigger);
        }
        else
        {
            var results = await retriever.GetCreateFunctionsAsync([first, second], TestContext.Current.CancellationToken);
            Assert.Equal("first definition", results[first].CreateFunction);
            Assert.Equal("second definition", results[second].CreateFunction);
        }

        string type = triggers ? "TRIGGER" : "FUNCTION";
        Assert.Contains($"SHOW CREATE {type} `first``schema`.`same``name`;", connection.CommandText);
        Assert.Contains($"SHOW CREATE {type} `second`.`same``name`;", connection.CommandText);
        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    [Fact]
    public async Task EmptyBatch_DoesNotOpenConnection()
    {
        var connection = new FakeConnection();
        using var context = CreateContext(connection);
        using var retriever = new RawEntityRetriever(context);

        Assert.Empty(await retriever.GetCreateTriggersAsync([], TestContext.Current.CancellationToken));
        Assert.Empty(await retriever.GetCreateFunctionsAsync([], TestContext.Current.CancellationToken));
        Assert.Equal(0, connection.OpenCount);
    }

    [Fact]
    public async Task Batch_LeavesOpenConnectionOpen()
    {
        var connection = new FakeConnection();
        connection.Results.Tables.Add(Result("Function", "Create Function", "first", "definition"));
        connection.Open();
        using var context = CreateContext(connection);
        using var retriever = new RawEntityRetriever(context);
        ObjectIdentifier first = ObjectIdentifier.FromStrings("def", "app", "first");

        var results = await retriever.GetCreateFunctionsAsync([first], TestContext.Current.CancellationToken);
        Assert.Equal("definition", results[first].CreateFunction);
        Assert.Equal(ConnectionState.Open, connection.State);
    }

    [Fact]
    public async Task ClosingFailure_DoesNotLeaveGateLocked()
    {
        var connection = new FakeConnection { FailClose = true };
        connection.Results.Tables.Add(Result("Trigger", "SQL Original Statement", "t", "definition"));
        using var context = CreateContext(connection);
        using var retriever = new RawEntityRetriever(context);
        ObjectIdentifier id = ObjectIdentifier.FromStrings("def", "app", "t");

        await Assert.ThrowsAsync<InvalidOperationException>(() => retriever.GetCreateTriggersAsync([id], TestContext.Current.CancellationToken));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var results = await retriever.GetCreateTriggersAsync([id], timeout.Token);
        Assert.Equal("definition", results[id].CreateTrigger);
    }

    private static DbContext CreateContext(DbConnection connection)
    {
        var options = new DbContextOptionsBuilder()
            .UseMySql(connection, new MySqlServerVersion(new Version(8, 0, 0)))
            .Options;
        return new DbContext(options);
    }

    private static DataTable Result(string nameColumn, string createColumn, string name, string definition)
    {
        var table = new DataTable();
        table.Columns.Add(nameColumn, typeof(string));
        table.Columns.Add("sql_mode", typeof(string));
        table.Columns.Add(createColumn, typeof(string));
        table.Rows.Add(name, "STRICT_TRANS_TABLES", definition);
        return table;
    }

    private sealed class FakeConnection : DbConnection
    {
        private ConnectionState _state;
        public DataSet Results { get; } = new();
        public string CommandText { get; set; } = "";
        public int OpenCount { get; private set; }
        public bool FailClose { get; set; }
        [AllowNull]
        public override string ConnectionString { get; set; } = "";
        public override string Database => "test";
        public override string DataSource => "test";
        public override string ServerVersion => "8.0.0";
        public override ConnectionState State => _state;

        public override void Open()
        {
            OpenCount++;
            _state = ConnectionState.Open;
        }

        public override void Close()
        {
            _state = ConnectionState.Closed;
            if (FailClose)
            {
                FailClose = false;
                throw new InvalidOperationException("Close failed.");
            }
        }

        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => new FakeCommand(this);
    }

    private sealed class FakeCommand(FakeConnection connection) : DbCommand
    {
        [AllowNull]
        public override string CommandText { get; set; } = "";
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get; set; } = connection;
        protected override DbParameterCollection DbParameterCollection => throw new NotSupportedException();
        protected override DbTransaction? DbTransaction { get; set; }
        public override void Cancel() => throw new NotSupportedException();
        public override int ExecuteNonQuery() => throw new NotSupportedException();
        public override object ExecuteScalar() => throw new NotSupportedException();
        public override void Prepare() => throw new NotSupportedException();
        protected override DbParameter CreateDbParameter() => throw new NotSupportedException();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            connection.CommandText = CommandText;
            return connection.Results.CreateDataReader();
        }
    }
}
