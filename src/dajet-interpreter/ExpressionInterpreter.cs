using DaJet.Scripting.Model;
using System.Globalization;

namespace DaJet.Scripting
{
    public sealed class ExpressionInterpreter
    {
        private readonly Dictionary<string, object> _data;
        public ExpressionInterpreter(in Dictionary<string, object> data)
        {
            ArgumentNullException.ThrowIfNull(data, nameof(data));

            _data = data;
        }
        public object Evaluate(in SyntaxNode expression)
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
        private object Evaluate(in ScalarExpression node)
        {
            string literal = node.Literal;

            if (node.Token == Token.Boolean)
            {
                return node.Literal == "TRUE";
            }
            else if (node.Token == Token.Integer)
            {
                return int.Parse(node.Literal);
            }
            else if (node.Token == Token.Decimal)
            {
                return decimal.Parse(node.Literal, CultureInfo.InvariantCulture);
            }
            else if (node.Token == Token.DateTime)
            {
                return DateTime.Parse(node.Literal);
            }
            else if (node.Token == Token.String)
            {
                return node.Literal;
            }
            else if (node.Token == Token.Binary)
            {
                return Convert.FromHexString(node.Literal);
            }
            else if (node.Token == Token.Uuid)
            {
                return new Guid(node.Literal);
            }

            return null;
        }
        private object Evaluate(in VariableReference node)
        {
            if (_data.TryGetValue(node.Identifier, out object value))
            {
                return value;
            }
            
            return null;
        }
        private object Evaluate(in MemberAccessExpression node)
        {
            List<string> members = node.GetAccessMembers(node.Identifier);

            if (_data.TryGetValue(members[0], out object value))
            {
                if (value is Dictionary<string, object> _object)
                {
                    if (_object.TryGetValue(members[1], out value))
                    {
                        return value;
                    }
                }
            }

            return null;
        }
        private object Evaluate(in FunctionExpression node)
        {
            if (!DaJetFunctions.TryGet(node.Name, out DaJetFunction function))
            {
                throw new InvalidOperationException($"Unknown function name: {node.Name}");
            }

            return FunctionInterpreter.Evaluate(this, in node);
        }
    }
}