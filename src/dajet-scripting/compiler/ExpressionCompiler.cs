using DaJet.Scripting.Model;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;

namespace DaJet.Scripting
{
    internal sealed class ExpressionCompiler
    {
        // Ссылка на объект, свойства которого используются
        // для вычисления значений переменных/свойств скрипта.
        private readonly FieldInfo _data;
        // Свойства объекта, ссылка на который хранится в поле _data
        private readonly Dictionary<string, PropertyInfo> _properties;
        internal ExpressionCompiler(in FieldInfo data, in Dictionary<string, PropertyInfo> properties)
        {
            _data = data;
            _properties = properties;
        }
        ///<summary>Evaluates expression and pushes returned value onto stack</summary>
        ///<returns>Expected type of the value returned by expression</returns>
        internal Type Evaluate(in SyntaxNode node, in ILGenerator IL)
        {
            if (node is ScalarExpression scalar) { return Evaluate(in scalar, in IL); }
            else if (node is VariableReference variable) { return Evaluate(in variable, in IL); }
            else if (node is MemberAccessExpression member) { return Evaluate(in member, in IL); }
            else if (node is FunctionExpression function) { return Evaluate(in function, in IL); }
            else if (node is GroupOperator grouping) { return Evaluate(in grouping, in IL); }
            else if (node is AdditionOperator addition) { return Evaluate(in addition, in IL); }
            else if (node is MultiplyOperator multiply) { return Evaluate(in multiply, in IL); }
            else if (node is UnaryOperator unary) { return Evaluate(in unary, in IL); }
            else if (node is BinaryOperator binary) { return Evaluate(in binary, in IL); }
            else if (node is ComparisonOperator comparison) { return Evaluate(in comparison, in IL); }

            return null; // failed to evaluate expression
        }
        internal Type Evaluate(in ScalarExpression node, in ILGenerator IL)
        {
            Type type = null;

            if (node.Token == Token.Boolean)
            {
                if (node.Literal == "TRUE")
                {
                    IL.Emit(OpCodes.Ldc_I4_1);
                }
                else
                {
                    IL.Emit(OpCodes.Ldc_I4_0);
                }

                type = typeof(bool);
            }
            else if (node.Token == Token.Integer)
            {
                if (int.TryParse(node.Literal, out int integer))
                {
                    IL.Emit(OpCodes.Ldc_I4, integer);
                }

                type = typeof(int);
            }
            else if (node.Token == Token.Number || node.Token == Token.Decimal)
            {
                ConstructorInfo DecimalCtor = typeof(decimal).GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public,
                    [typeof(int), typeof(int), typeof(int), typeof(bool), typeof(byte)]);

                if (decimal.TryParse(node.Literal, CultureInfo.InvariantCulture, out decimal number))
                {
                    int[] bits = decimal.GetBits(number);
                    bool negative = (bits[3] & 0x80000000) != 0;
                    int scale = (byte)((bits[3] >> 16) & 0x7f);
                    IL.Emit(OpCodes.Ldc_I4, bits[0]);
                    IL.Emit(OpCodes.Ldc_I4, bits[1]);
                    IL.Emit(OpCodes.Ldc_I4, bits[2]);
                    IL.Emit(negative ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                    IL.Emit(OpCodes.Ldc_I4, scale);
                    IL.Emit(OpCodes.Newobj, DecimalCtor);
                }

                type = typeof(decimal);
            }
            else if (node.Token == Token.DateTime)
            {
                ConstructorInfo DateTimeCtor = typeof(DateTime).GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public,
                    [typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int)]);

                if (DateTime.TryParse(node.Literal, out DateTime datetime))
                {
                    IL.Emit(OpCodes.Ldc_I4, datetime.Year);
                    IL.Emit(OpCodes.Ldc_I4, datetime.Month);
                    IL.Emit(OpCodes.Ldc_I4, datetime.Day);
                    IL.Emit(OpCodes.Ldc_I4, datetime.Hour);
                    IL.Emit(OpCodes.Ldc_I4, datetime.Minute);
                    IL.Emit(OpCodes.Ldc_I4, datetime.Second);
                    IL.Emit(OpCodes.Newobj, DateTimeCtor);
                }

                type = typeof(DateTime);
            }
            else if (node.Token == Token.String)
            {
                IL.Emit(OpCodes.Ldstr, node.Literal);

                type = typeof(string);
            }
            else if (node.Token == Token.Binary)
            {
                byte[] hex = Convert.FromHexString(node.Literal);

                //IL.Emit(OpCodes.Stelem, typeof(byte));

                type = typeof(byte[]);
            }
            else if (node.Token == Token.Uuid)
            {
                ConstructorInfo GuidCtor = typeof(Guid).GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public,
                    [typeof(string)]);

                IL.Emit(OpCodes.Ldstr, node.Literal);
                IL.Emit(OpCodes.Newobj, GuidCtor);

                type = typeof(Guid);
            }

            return type;
        }
        internal Type Evaluate(in VariableReference node, in ILGenerator IL)
        {
            IL.Emit(OpCodes.Ldarg_0); // ScriptProcessor : this (without field)

            if (_data is not null) // SelectProcessor : this._data
            {
                IL.Emit(OpCodes.Ldfld, _data);
            }

            if (_properties.TryGetValue(node.Identifier, out PropertyInfo property))
            {
                MemberInfo getAccessor = property.GetGetMethod();

                IL.Emit(OpCodes.Callvirt, property.GetGetMethod());
            }

            return property.PropertyType;
        }
        internal Type Evaluate(in MemberAccessExpression node, in ILGenerator IL)
        {
            List<string> members = node.GetAccessMembers(node.Identifier);

            if (members.Count > 2) // TODO: allow more members
            {
                throw new InvalidOperationException("Too many members");
            }

            VariableReference variable = new() { Identifier = members[0] };

            Type source = Evaluate(in variable, in IL);

            PropertyInfo property = source.GetProperty(members[1],
                BindingFlags.Instance | BindingFlags.Public);

            IL.Emit(OpCodes.Callvirt, property.GetGetMethod());

            source = property.PropertyType;

            return source;
        }
        internal Type Evaluate(in FunctionExpression node, in ILGenerator IL)
        {
            return null;
        }
        internal Type Evaluate(in GroupOperator node, in ILGenerator IL)
        {
            return null;
        }
        internal Type Evaluate(in AdditionOperator node, in ILGenerator IL)
        {
            return null;
        }
        internal Type Evaluate(in MultiplyOperator node, in ILGenerator IL)
        {
            return null;
        }
        internal Type Evaluate(in UnaryOperator node, in ILGenerator IL)
        {
            // NOT -

            return null;
        }
        internal Type Evaluate(in BinaryOperator node, in ILGenerator IL)
        {
            // OR AND || &&

            return null;
        }
        internal Type Evaluate(in ComparisonOperator node, in ILGenerator IL)
        {
            //else if (token == Token.Equals) { return "="; }
            //else if (token == Token.NotEquals) { return "<>"; }
            //else if (token == Token.Less) { return "<"; }
            //else if (token == Token.LessOrEquals) { return "<="; }
            //else if (token == Token.Greater) { return ">"; }
            //else if (token == Token.GreaterOrEquals) { return ">="; }

            return null;
        }
    }
}