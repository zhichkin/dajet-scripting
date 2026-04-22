using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Reflection;
using System.Reflection.Emit;

namespace DaJet.Compiler
{
    internal sealed class ExpressionCompiler
    {
        // Ссылка на объект, свойства которого используются
        // для вычисления значений переменных/свойств скрипта.
        private readonly Type _script;
        private readonly FieldInfo _context;
        private readonly Dictionary<string, PropertyInfo> _properties;
        internal ExpressionCompiler(in Type script, in Dictionary<string, PropertyInfo> properties)
        {
            _script = script;
            _properties = properties;
        }
        internal ExpressionCompiler(in FieldInfo context, in Dictionary<string, PropertyInfo> properties)
        {
            _context = context;
            _properties = properties;
        }
        ///<summary>Evaluates expression and pushes value onto stack</summary>
        ///<returns>Type returned by expression</returns>
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
            return null;
        }
        internal Type Evaluate(in VariableReference node, in ILGenerator IL)
        {
            //IL.Emit(OpCodes.Ldarg_0); // ScriptProcessor : this (without field)

            if (_context is not null) // SelectProcessor : this._context
            {
                //IL.Emit(OpCodes.Ldfld, _context);
            }

            string propertyName = node.Identifier.TrimStart('@');

            if (_properties.TryGetValue(node.Identifier, out PropertyInfo property))
            {
                MemberInfo getAccessor = property.GetGetMethod();

                //IL.Emit(OpCodes.Callvirt, property.GetGetMethod());
            }

            return property.PropertyType;
        }
        internal Type Evaluate(in MemberAccessExpression node, in ILGenerator IL)
        {
            return null;
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