using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Postgres.Tests.IO.Formatting;

public class FormatCreateTriggerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TriggerFormatting_PreservesWhenAndExecutionSyntax(bool useProcedure)
    {
        var execute = new PgTriggerExecute(new ObjectName(new Identifier("fire")),
            [new PgStatementTriggerArgument.Literal(new Value.SingleQuotedString("value"))], useProcedure);
        var trigger = new CreateTrigger(new ObjectName(new Identifier("audit")), TriggerTime.After,
            [new TriggerEvent.Update([new Identifier("id")])], new ObjectName(new Identifier("items")), execute)
        {
            PgOptions = new PgStatementTriggerOptions
            {
                ConstraintOptions = useProcedure ? new PgTriggerConstraint(new ObjectName(new Identifier("parent")), true, true) : null,
                When = new LiteralValue(new Value.Boolean(true))
            },
            ExecutionQuantifier = new TriggerExecutionQuantifier.ForEach(TriggerForEachType.Row)
        };
        using var formatter = Helpers.CreateFormatter();

        string formatted = formatter.GetFormatted(trigger.ToSql());

        Assert.Contains("WHEN (TRUE)", formatted);
        Assert.Contains(useProcedure ? "EXECUTE PROCEDURE " : "EXECUTE FUNCTION ", formatted);
        Assert.Contains("('value')", formatted);
        if (useProcedure)
        {
            Assert.Contains("CONSTRAINT TRIGGER ", formatted);
            Assert.Contains(" DEFERRABLE INITIALLY DEFERRED", formatted);
        }
    }
}
