using DaJet.Data;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Collections;
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
            else if (expression is ValuesExpression array) { return Evaluate(in array); }

            else if (expression is GroupOperator grouping) { return Evaluate(in grouping); }
            else if (expression is MultiplyOperator multiply) { return Evaluate(in multiply); }
            else if (expression is AdditionOperator addition) { return Evaluate(in addition); }
            else if (expression is UnaryOperator unary) { return Evaluate(in unary); }
            else if (expression is BinaryOperator binary) { return Evaluate(in binary); }
            else if (expression is ComparisonOperator comparison) { return Evaluate(in comparison); }

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
            else if (node.Token == Token.Entity)
            {
                return Entity.Parse(node.Literal);
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
            List<string> members = node.GetAccessMembers();

            if (_data.TryGetValue(members[0], out object value))
            {
                if (value is DataObject data)
                {
                    if (data.TryGetValue(members[1], out value))
                    {
                        return value;
                    }
                }
            }

            return null;
        }
        private object Evaluate(in FunctionExpression node)
        {
            if (!DaJetFunctions.TryGet(node.Name, out Function function))
            {
                throw new InvalidOperationException($"Unknown function name: {node.Name}");
            }

            return FunctionInterpreter.Evaluate(this, in node);
        }
        private object Evaluate(in ValuesExpression node)
        {
            List<SyntaxNode> values = node.Values;

            if (values.Count == 0)
            {
                return new List<object>(); // empty array
            }
            
            SyntaxNode item = values[0];

            DataType type = item.InferType();

            type = DataType.Array(type);

            IList array = type.DefaultValue() as IList;

            for (int i = 0; i < values.Count; i++)
            {
                item = values[i];

                object value = Evaluate(item);

                array.Add(value);
            }

            return array is not null ? array : new List<object>(); // empty array
        }

        private object Evaluate(in GroupOperator node)
        {
            return Evaluate(node.Expression);
        }
        private object Evaluate(in MultiplyOperator node)
        {
            if (!(node.Token == Token.Multiply || node.Token == Token.Divide || node.Token == Token.Modulo))
            {
                throw new InvalidOperationException($"Unsupported multiply operator {node.Token}");
            }

            object left = Evaluate(node.Expression1);
            object right = Evaluate(node.Expression2);

            if (left is int int1 && right is int int2)
            {
                if (node.Token == Token.Multiply) { return int1 * int2; }
                else if (node.Token == Token.Divide) { return int1 / int2; }
                else if (node.Token == Token.Modulo) { return int1 % int2; }
            }

            if (left is long long1 && right is long long2)
            {
                if (node.Token == Token.Multiply) { return long1 * long2; }
                else if (node.Token == Token.Divide) { return long1 / long2; }
                else if (node.Token == Token.Modulo) { return long1 % long2; }
            }

            if (left is long l1 && right is int i1)
            {
                if (node.Token == Token.Multiply) { return l1 * Convert.ToInt64(i1); }
                else if (node.Token == Token.Divide) { return l1 / Convert.ToInt64(i1); }
                else if (node.Token == Token.Modulo) { return l1 % Convert.ToInt64(i1); }
            }

            if (left is int i2 && right is long l2)
            {
                if (node.Token == Token.Multiply) { return Convert.ToInt64(i2) * l2; }
                else if (node.Token == Token.Divide) { return Convert.ToInt64(i2) / l2; }
                else if (node.Token == Token.Modulo) { return Convert.ToInt64(i2) % l2; }
            }

            if (left is decimal dec1 && right is decimal dec2)
            {
                if (node.Token == Token.Multiply) { return dec1 * dec2; }
                else if (node.Token == Token.Divide) { return dec1 / dec2; }
                else if (node.Token == Token.Modulo) { return dec1 % dec2; }
            }

            throw new InvalidOperationException($"Unsupported multiply operation [{node.Token}]");
        }
        private object Evaluate(in AdditionOperator node)
        {
            object left = Evaluate(node.Expression1);
            object right = Evaluate(node.Expression2);

            if (left is int int1 && right is int int2)
            {
                return node.Token == Token.Plus ? int1 + int2 : int1 - int2;
            }
            
            if (left is long long1 && right is long long2)
            {
                return node.Token == Token.Plus ? long1 + long2 : long1 - long2;
            }
            
            if (left is long l1 && right is int i1)
            {
                return node.Token == Token.Plus
                    ? l1 + Convert.ToInt64(i1)
                    : l1 - Convert.ToInt64(i1);
            }
            
            if (left is int i2 && right is long l2)
            {
                return node.Token == Token.Plus
                    ? Convert.ToInt64(i2) + l2
                    : Convert.ToInt64(i2) - l2;
            }
            
            if (left is decimal dec1 && right is decimal dec2)
            {
                return node.Token == Token.Plus ? dec1 + dec2 : dec1 - dec2;
            }

            return ToStringValue(in left) + ToStringValue(in right);
        }
        private static string ToStringValue(in object value)
        {
            if (value is null)
            {
                return "null";
            }
            else if (value is bool boolean)
            {
                return boolean ? "true" : "false";
            }
            else if (value is decimal number)
            {
                return number.ToString().Replace(',', '.');
            }
            else if (value is DateTime datetime)
            {
                return datetime.ToString("yyyy-MM-dd HH:mm:ss");
            }
            else if (value is byte[] binary)
            {
                string hex = "0x";

                if (binary.Length == 0)
                {
                    return hex;
                }
                else
                {
                    return hex + DbUtilities.ByteArrayToString(binary);
                }
            }

            return value.ToString();
        }
        private object Evaluate(in UnaryOperator node)
        {
            object value = Evaluate(node.Expression);

            if (node.Token == Token.NOT)
            {
                if (value is not bool boolean)
                {
                    throw new InvalidOperationException("Boolean value expected");
                }

                return !boolean;
            }

            if (value is int integer)
            {
                return -integer;
            }
            else if (value is decimal numeric)
            {
                return -numeric;
            }

            throw new InvalidOperationException("Numeric value expected");
        }
        private object Evaluate(in BinaryOperator node)
        {
            object value = Evaluate(node.Expression1);

            if (value is not bool boolean)
            {
                throw new InvalidOperationException("Boolean value expected");
            }

            if (node.Token == Token.OR)
            {
                if (boolean)
                {
                    return true;
                }

                return Evaluate(node.Expression2);
            }

            if (node.Token == Token.AND)
            {
                if (!boolean)
                {
                    return false;
                }

                return Evaluate(node.Expression2);
            }

            throw new InvalidOperationException($"Unknown binary operator [{node.Token}]");
        }
        private object Evaluate(in ComparisonOperator node)
        {
            object left = Evaluate(node.Expression1);
            object right = Evaluate(node.Expression2);

            if (left is int left_int32)
            {
                if (right is null)
                {
                    throw new InvalidOperationException($"Unsupported comparison operator: int32 {node.Token} null");
                }
                else if (right is int right_int32)
                {
                    return CompareNumbers(node.Token, left_int32, right_int32);
                }
                else if (right is long right_int64)
                {
                    return CompareNumbers(node.Token, left_int32, right_int64);
                }
                else if (right is decimal right_dec64)
                {
                    return CompareNumbers(node.Token, left_int32, right_dec64);
                }
                else
                {
                    throw new InvalidOperationException($"Unsupported comparison operator: int32 {node.Token} {right.GetType()}");
                }
            }
            else if (left is long left_int64)
            {
                if (right is null)
                {
                    throw new InvalidOperationException($"Unsupported comparison operator: int64 {node.Token} null");
                }
                else if (right is int right_int32)
                {
                    return CompareNumbers(node.Token, left_int64, right_int32);
                }
                else if (right is long right_int64)
                {
                    return CompareNumbers(node.Token, left_int64, right_int64);
                }
                else if (right is decimal right_dec64)
                {
                    return CompareNumbers(node.Token, left_int64, right_dec64);
                }
                else
                {
                    throw new InvalidOperationException($"Unsupported comparison operator: int64 {node.Token} {right.GetType()}");
                }
            }
            else if (left is decimal left_dec64)
            {
                if (right is null)
                {
                    throw new InvalidOperationException($"Unsupported comparison operator: dec64 {node.Token} null");
                }
                else if (right is int right_int32)
                {
                    return CompareNumbers(node.Token, left_dec64, right_int32);
                }
                else if (right is long right_int64)
                {
                    return CompareNumbers(node.Token, left_dec64, right_int64);
                }
                else if (right is decimal right_dec64)
                {
                    return CompareNumbers(node.Token, left_dec64, right_dec64);
                }
                else
                {
                    throw new InvalidOperationException($"Unsupported comparison operator: dec64 {node.Token} {right.GetType()}");
                }
            }
            else if (left is DateTime left_date && right is DateTime right_date)
            {
                return CompareDateTime(node.Token, left_date, right_date);
            }

            string first = left is null ? string.Empty : left.ToString();
            string second = right is null ? string.Empty : right.ToString();

            if (node.Token == Token.Equals)
            {
                return first == second;
            }
            else if (node.Token == Token.NotEquals)
            {
                return first != second;
            }

            throw new InvalidOperationException($"Unknown comparison operator [{node.Token}]");
        }
        private static bool CompareNumbers(Token _operator, int left, int right)
        {
            if (_operator == Token.Equals) { return left == right; }
            else if (_operator == Token.Less) { return left < right; }
            else if (_operator == Token.Greater) { return left > right; }
            else if (_operator == Token.NotEquals) { return left != right; }
            else if (_operator == Token.LessOrEquals) { return left <= right; }
            else if (_operator == Token.GreaterOrEquals) { return left >= right; }
            
            throw new InvalidOperationException($"Unknown comparison operator [{_operator}]");
        }
        private static bool CompareNumbers(Token _operator, int left, long right)
        {
            if (_operator == Token.Equals) { return left == right; }
            else if (_operator == Token.Less) { return left < right; }
            else if (_operator == Token.Greater) { return left > right; }
            else if (_operator == Token.NotEquals) { return left != right; }
            else if (_operator == Token.LessOrEquals) { return left <= right; }
            else if (_operator == Token.GreaterOrEquals) { return left >= right; }
            
            throw new InvalidOperationException($"Unknown comparison operator [{_operator}]");
        }
        private static bool CompareNumbers(Token _operator, long left, int right)
        {
            if (_operator == Token.Equals) { return left == right; }
            else if (_operator == Token.Less) { return left < right; }
            else if (_operator == Token.Greater) { return left > right; }
            else if (_operator == Token.NotEquals) { return left != right; }
            else if (_operator == Token.LessOrEquals) { return left <= right; }
            else if (_operator == Token.GreaterOrEquals) { return left >= right; }
            
            throw new InvalidOperationException($"Unknown comparison operator [{_operator}]");
        }
        private static bool CompareNumbers(Token _operator, long left, long right)
        {
            if (_operator == Token.Equals) { return left == right; }
            else if (_operator == Token.Less) { return left < right; }
            else if (_operator == Token.Greater) { return left > right; }
            else if (_operator == Token.NotEquals) { return left != right; }
            else if (_operator == Token.LessOrEquals) { return left <= right; }
            else if (_operator == Token.GreaterOrEquals) { return left >= right; }
            
            throw new InvalidOperationException($"Unknown comparison operator [{_operator}]");
        }
        private static bool CompareNumbers(Token _operator, decimal left, decimal right)
        {
            if (_operator == Token.Equals) { return left == right; }
            else if (_operator == Token.Less) { return left < right; }
            else if (_operator == Token.Greater) { return left > right; }
            else if (_operator == Token.NotEquals) { return left != right; }
            else if (_operator == Token.LessOrEquals) { return left <= right; }
            else if (_operator == Token.GreaterOrEquals) { return left >= right; }
            
            throw new InvalidOperationException($"Unknown comparison operator [{_operator}]");
        }
        private static bool CompareNumbers(Token _operator, int left, decimal right)
        {
            if (_operator == Token.Equals) { return left == right; }
            else if (_operator == Token.Less) { return left < right; }
            else if (_operator == Token.Greater) { return left > right; }
            else if (_operator == Token.NotEquals) { return left != right; }
            else if (_operator == Token.LessOrEquals) { return left <= right; }
            else if (_operator == Token.GreaterOrEquals) { return left >= right; }
            
            throw new InvalidOperationException($"Unknown comparison operator [{_operator}]");
        }
        private static bool CompareNumbers(Token _operator, decimal left, int right)
        {
            if (_operator == Token.Equals) { return left == right; }
            else if (_operator == Token.Less) { return left < right; }
            else if (_operator == Token.Greater) { return left > right; }
            else if (_operator == Token.NotEquals) { return left != right; }
            else if (_operator == Token.LessOrEquals) { return left <= right; }
            else if (_operator == Token.GreaterOrEquals) { return left >= right; }
            
            throw new InvalidOperationException($"Unknown comparison operator [{_operator}]");
        }
        private static bool CompareNumbers(Token _operator, long left, decimal right)
        {
            if (_operator == Token.Equals) { return left == right; }
            else if (_operator == Token.Less) { return left < right; }
            else if (_operator == Token.Greater) { return left > right; }
            else if (_operator == Token.NotEquals) { return left != right; }
            else if (_operator == Token.LessOrEquals) { return left <= right; }
            else if (_operator == Token.GreaterOrEquals) { return left >= right; }
            
            throw new InvalidOperationException($"Unknown comparison operator [{_operator}]");
        }
        private static bool CompareNumbers(Token _operator, decimal left, long right)
        {
            if (_operator == Token.Equals) { return left == right; }
            else if (_operator == Token.Less) { return left < right; }
            else if (_operator == Token.Greater) { return left > right; }
            else if (_operator == Token.NotEquals) { return left != right; }
            else if (_operator == Token.LessOrEquals) { return left <= right; }
            else if (_operator == Token.GreaterOrEquals) { return left >= right; }
            
            throw new InvalidOperationException($"Unknown comparison operator [{_operator}]");
        }
        private static bool CompareDateTime(Token _operator, DateTime left, DateTime right)
        {
            if (_operator == Token.Equals) { return left == right; }
            else if (_operator == Token.Less) { return left < right; }
            else if (_operator == Token.Greater) { return left > right; }
            else if (_operator == Token.NotEquals) { return left != right; }
            else if (_operator == Token.LessOrEquals) { return left <= right; }
            else if (_operator == Token.GreaterOrEquals) { return left >= right; }
            
            throw new InvalidOperationException($"Unknown comparison operator [{_operator}]");
        }
    }
}