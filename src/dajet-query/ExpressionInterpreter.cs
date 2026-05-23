using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Data;

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
            string literal = scalar.Literal;

                        if (type.IsBoolean)
                        {
                            command.Parameters.AddWithValue(name, literal == "TRUE" ? TRUE : FALSE);
                        }
                        else if (type.IsDecimal)
                        {
                            command.Parameters.AddWithValue(name, decimal.Parse(literal));
                        }
                        else if (type.IsDateTime)
                        {
                            DateTime value = DateTime.Parse(literal).AddYears(_yearOffset);

                            command.Parameters.AddWithValue(name, value).SqlDbType = SqlDbType.DateTime2;
                        }
                        else if (type.IsString)
                        {
                            command.Parameters.AddWithValue(name, literal);
                        }
                        else if (type.IsUuid)
                        {
                            command.Parameters.AddWithValue(name, new Guid(literal));
                        }
                    }
                }
            }

            return null;
        }
        private object Evaluate(in VariableReference node)
        {
            if (node is VariableReference variable)
            {
                if (variable.Binding is DeclareStatement declare)
                {
                    DataType type = declare.Type;

                    if (declare.Initializer is ScalarExpression scalar)
                    {
                    }
                }
            }
            
            return null;
        }
        private object Evaluate(in MemberAccessExpression node)
        {
            return null;
        }
        private object Evaluate(in FunctionExpression node)
        {
            return null;
        }
    }
}