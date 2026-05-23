using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    internal sealed class ExpressionInterpreter
    {
        private readonly Dictionary<string, object> _data;
        internal ExpressionInterpreter(in Dictionary<string, object> data)
        {
            ArgumentNullException.ThrowIfNull(data, nameof(data));

            _data = data;
        }
        internal object Evaluate(in SyntaxNode expression)
        {
            if (expression is null) { return null; }
            else if (expression is ScalarExpression scalar) { return Evaluate(in scalar); }
            else if (expression is VariableReference variable) { return Evaluate(in variable); }
            else if (expression is MemberAccessExpression member) { return Evaluate(in member); }
            else if (expression is FunctionExpression function) { return Evaluate(in function); }
            //else if (expression is GroupOperator grouping) { return Evaluate(in grouping); }
            //else if (expression is AdditionOperator addition) { return Evaluate(in addition); }
            //else if (expression is MultiplyOperator multiply) { return Evaluate(in multiply); }
            //else if (expression is UnaryOperator unary) { return Evaluate(in unary); }
            //else if (expression is BinaryOperator binary) { return Evaluate(in binary); }
            //else if (expression is ComparisonOperator comparison) { return Evaluate(in comparison); }

            return null; // unsupported expression type
        }
        internal object Evaluate(in ScalarExpression node)
        {
            return null;
        }
        internal object Evaluate(in VariableReference node)
        {
            return null;
        }
        internal object Evaluate(in MemberAccessExpression node)
        {
            return null;
        }
        internal object Evaluate(in FunctionExpression node)
        {
            return null;
        }
    }
}