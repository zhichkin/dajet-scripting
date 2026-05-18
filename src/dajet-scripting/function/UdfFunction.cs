using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Reflection.Emit;

namespace DaJet.Scripting
{
    public abstract class UdfFunction
    {
        public abstract DataType GetReturnType(in FunctionExpression node);
        internal abstract Type Evaluate(in ExpressionCompiler context, in FunctionExpression node, in ILGenerator IL);

        //public virtual void Visit(in FunctionExpression node, in StringBuilder script, in IStatementTranspiler statement)
        //{
        //    if (node.Token == Token.UDF)
        //    {
        //        throw new InvalidOperationException($"Invalid function name: {node.Name}");
        //    }

        //    script.Append(node.Name);

        //    if (node.Token != Token.EXISTS)
        //    {
        //        script.Append('('); //NOTE: EXISTS function has one parameter - TableExpression
        //    }

        //    if (node.Token == Token.COUNT && node.Modifier == Token.DISTINCT)
        //    {
        //        script.Append("DISTINCT "); //TODO: SUM(DISTINCT )
        //    }

        //    SyntaxNode expression;

        //    for (int i = 0; i < node.Parameters.Count; i++)
        //    {
        //        expression = node.Parameters[i];

        //        if (i > 0) { script.Append(", "); }

        //        statement.Visit(in expression, in script);
        //    }

        //    if (node.Token != Token.EXISTS)
        //    {
        //        script.Append(')'); //NOTE: EXISTS function has one parameter - TableExpression
        //    }

        //    if (node.Over is not null)
        //    {
        //        script.Append(' ');

        //        statement.Visit(node.Over, in script);
        //    }
        //}
    }
}