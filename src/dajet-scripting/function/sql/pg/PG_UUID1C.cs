using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    internal sealed class PG_UUID1C : Function
    {
        public override DataType GetReturnType(in FunctionExpression node)
        {
            return DataType.Uuid();
        }
        public override void Transpile(in SqlTranspiler statement, in FunctionExpression node, in StringBuilder script)
        {
            SyntaxNode parameter = node.Parameters[0];

            if (parameter is not ColumnReference column)
            {
                throw new InvalidOperationException($"[UUID1C] The first parameter must be a column reference.");
            }

            if (column.Binding is not PropertyDefinition property)
            {
                //TODO: process union type columns
            }

            script.Append("REVERSE(SUBSTRING(");

            statement.Visit(in parameter, in script); // column reference

            script.Append(", 9, 8)) || SUBSTRING(");

            statement.Visit(in parameter, in script); // column reference

            script.Append(", 1, 8)");
        }
    }
}