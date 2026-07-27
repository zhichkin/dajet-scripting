using DaJet.Scripting.Model;
using System.Collections.Frozen;
using System.Runtime.CompilerServices;

namespace DaJet.Scripting
{
    internal static class ReadOnlyStatements
    {
        private static readonly FrozenSet<Type> _statements = FrozenSet.ToFrozenSet(
        [
            typeof(CommentStatement),
            typeof(DeclareStatement),
            typeof(UseStatement),
            typeof(SelectStatement),
            typeof(AssignmentOperator),
            typeof(ReturnStatement),
            typeof(PrintStatement)
        ]);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool Contains(in SyntaxNode node)
        {
            return _statements.Contains(node.GetType());
        }
    }
}