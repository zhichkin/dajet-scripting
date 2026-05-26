using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;

namespace DaJet.Scripting
{
    public abstract class Function
    {
        public abstract DataType GetReturnType(in FunctionExpression node);
        public virtual void Transpile(in SqlTranspiler statement, in FunctionExpression node, in StringBuilder script)
        {
            if (node.Token == Token.UDF)
            {
                throw new InvalidOperationException($"Invalid SQL function name: {node.Name}");
            }

            script.Append(node.Name);

            if (node.Token != Token.EXISTS)
            {
                script.Append('('); //NOTE: EXISTS function has one parameter - TableExpression
            }

            if (node.Token == Token.COUNT && node.Modifier == Token.DISTINCT)
            {
                script.Append("DISTINCT "); //TODO: SUM(DISTINCT )
            }

            SyntaxNode expression;

            for (int i = 0; i < node.Parameters.Count; i++)
            {
                expression = node.Parameters[i];

                if (i > 0) { script.Append(", "); }

                statement.Visit(in expression, in script);
            }

            if (node.Token != Token.EXISTS)
            {
                script.Append(')'); //NOTE: EXISTS function has one parameter - TableExpression
            }

            if (node.Over is not null)
            {
                script.Append(' ');

                statement.Visit(node.Over, in script);
            }
        }
        
        //public abstract object Compile(in FunctionExpression node);
        //public abstract object Evaluate(in FunctionExpression node);
    }
}