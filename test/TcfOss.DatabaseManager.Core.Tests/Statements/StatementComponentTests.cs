using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;
using CreateOrLabel = TcfOss.DatabaseManager.Core.Statements.LabelAttributes.CreateOrLabel;

namespace TcfOss.DatabaseManager.Core.Tests.Statements;

public class StatementComponentTests
{
    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<SetQuantifier>))]
    public void SetQuantifier_FromText(string text, SetQuantifier expected)
    {
        var result = SetQuantifier.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void SetQuantifier_FromText_Unknown_Throws()
    {
        Assert.Throws<ArgumentException>(() => SetQuantifier.Parse("UNKNOWN"));
    }

    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<CreateOrLabel>))]
    public void CreateOrLabel_FromText(string text, CreateOrLabel expected)
    {
        var result = CreateOrLabel.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void CreateOrLabel_FromText_Unknown_Throws()
    {
        Assert.Throws<ArgumentException>(() => CreateOrLabel.Parse("UNKNOWN"));
    }

    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<HandlerAction>))]
    public void HandlerAction_FromText(string text, HandlerAction expected)
    {
        var result = HandlerAction.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void HandlerAction_FromText_Unknown_Throws()
    {
        Assert.Throws<ArgumentException>(() => HandlerAction.Parse("UNKNOWN"));
    }

    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<DuplicateTreatment>))]
    public void DuplicateTreatment_FromText(string text, DuplicateTreatment expected)
    {
        var result = DuplicateTreatment.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void DuplicateTreatment_FromText_Unknown_Throws()
    {
        Assert.Throws<ArgumentException>(() => DuplicateTreatment.Parse("UNKNOWN"));
    }

    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<MySqlTransactionCharacteristic>))]
    public void MySqlTransactionCharacteristic_FromText(string text, MySqlTransactionCharacteristic expected)
    {
        var result = MySqlTransactionCharacteristic.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void MySqlTransactionCharacteristic_FromText_Unknown_Throws()
    {
        Assert.Throws<ArgumentException>(() => MySqlTransactionCharacteristic.Parse("UNKNOWN"));
    }

    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<SetOperator>))]
    public void SetOperator_FromText(string text, SetOperator expected)
    {
        var result = SetOperator.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void SetOperator_FromText_Unknown_Throws()
    {
        Assert.Throws<ArgumentException>(() => SetOperator.Parse("UNKNOWN"));
    }

    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<ShowDiagnosticType>))]
    public void ShowDiagnosticType_FromText(string text, ShowDiagnosticType expected)
    {
        var result = ShowDiagnosticType.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void ShowDiagnosticType_FromText_Unknown_Throws()
    {
        Assert.Throws<ArgumentException>(() => ShowDiagnosticType.Parse("UNKNOWN"));
    }

    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<SignalPropertyName>))]
    public void SignalPropertyName_FromText(string text, SignalPropertyName expected)
    {
        var result = SignalPropertyName.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void SignalPropertyName_FromText_Unknown_Throws()
    {
        Assert.Throws<ArgumentException>(() => SignalPropertyName.Parse("UNKNOWN"));
    }

    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<TransactionNoun>))]
    public void TransactionNoun_FromText(string text, TransactionNoun expected)
    {
        var result = TransactionNoun.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void TransactionNoun_FromText_Unknown_Throws()
    {
        Assert.Throws<ArgumentException>(() => TransactionNoun.Parse("UNKNOWN"));
    }

    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<DeallocatePrepareLabel>))]
    public void DeallocatePrepareLabel_FromText(string text, DeallocatePrepareLabel expected)
    {
        var result = DeallocatePrepareLabel.Parse(text);
        Assert.Equal(expected.ToSql(), result.ToSql());
    }

    [Fact]
    public void DeallocatePrepareLabel_FromText_Unknown_Throws()
    {
        Assert.Throws<ArgumentException>(() => DeallocatePrepareLabel.Parse("UNKNOWN"));
    }
}
