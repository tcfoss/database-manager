using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Components;

public abstract record FunctionReturn : IWriteSql
{
    public abstract StatementFunctionReturn ToStatementReturn();

    public void ToSql(SqlTextWriter writer)
    {
        ToStatementReturn().ToSql(writer);
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        ToStatementReturn().FormatSql(writer, manager);
    }

    public static implicit operator FunctionReturn(DataType dataType)
    {
        return new DataTypeReturn(dataType);
    }

    public abstract record Element : FunctionReturn
    {
        public abstract override StatementFunctionReturn.Element ToStatementReturn();
    }

    public record DataTypeReturn(DataType DataType) : Element
    {
        public override StatementFunctionReturn.DataTypeReturn ToStatementReturn()
        {
            return new StatementFunctionReturn.DataTypeReturn(DataType);
        }
    }

    public record PseudoType(PgPseudoType Type) : Element
    {
        public override StatementFunctionReturn.DataTypeReturn ToStatementReturn()
        {
            return new StatementFunctionReturn.DataTypeReturn(new DataTypeReference.PgPseudoType(Type));
        }
    }

    public record SetOf(Element ReturnType) : FunctionReturn
    {
        public override StatementFunctionReturn.SetOf ToStatementReturn()
        {
            return new StatementFunctionReturn.SetOf(ReturnType.ToStatementReturn());
        }
    }

    public record Table(SqlValueList<TableColumn> Columns) : FunctionReturn
    {
        public override StatementFunctionReturn.Table ToStatementReturn()
        {
            return new StatementFunctionReturn.Table([.. Columns.Select(column => new StatementFunctionReturn.TableColumn(column.Name, column.DataType))]);
        }
    }

    public record TableColumn(Identifier Name, DataType DataType);
}
