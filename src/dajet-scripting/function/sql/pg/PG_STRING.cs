using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    internal sealed class PG_STRING : Function
    {
        public override DataType GetReturnType(in FunctionExpression node)
        {
            return DataType.String();
        }
        public override void Transpile(in SqlTranspiler statement, in FunctionExpression node, in StringBuilder script)
        {
            // LOWER ( <expression> )
            // UPPER ( <expression> )
            // LTRIM ( <expression> )
            // RTRIM ( <expression> )
            // CONCAT ( argument1 , argument2 [ , argumentN ] ... )
            // CONCAT_WS ( separator , argument1 , argument2 [ , argumentN ] ... )
            // REPLACE ( string_expression , string_pattern , string_replacement )
            // STRING_AGG ( expression , separator )

            script.Append(node.Name).Append('(');

            SyntaxNode parameter = node.Parameters[0];

            for (int i = 0; i < node.Parameters.Count; i++)
            {
                parameter = node.Parameters[i];

                if (i > 0) { script.Append(", "); }

                script.Append("CAST(");

                statement.Visit(in parameter, in script);

                script.Append(" AS text)");
            }

            script.Append(')');
        }
    }
}