using System.Text;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.Resources;

namespace TcfOss.DatabaseManager.Core.Errors;

public class IdentifierMismatchException(string message)
    : InvalidOperationException(message)
{
    public class ColumnParentMismatch(ColumnIdentifier columnIdentifier, ObjectIdentifier expectedParent)
        : IdentifierMismatchException(s_compositeFormat.Apply(columnIdentifier.Name, columnIdentifier.Table, expectedParent))
    {
        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Internal_ColumnIdentifierParentMismatch);
    }

    public class IdentifierLengthException(string identifierType, int requiredLength, int givenLength, IEnumerable<string>? components)
    : IdentifierMismatchException(s_compositeFormat.Apply(components.ToIdentifier(), givenLength, requiredLength, identifierType))
    {
        public IdentifierLengthException(string identifierType, int requiredLength, int givenLength, IEnumerable<Identifier> components)
            : this(identifierType, requiredLength, givenLength, components.Select(c => c.Name))
        { }

        private static readonly CompositeFormat s_compositeFormat = CompositeFormat.Parse(ErrorMessages.Err_Internal_IdentifierLengthMismatch);
    }
}
