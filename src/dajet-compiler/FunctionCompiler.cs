using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Reflection;
using System.Reflection.Emit;

namespace DaJet.Scripting
{
    internal static class FunctionCompiler
    {
        internal static Type Evaluate(in ExpressionCompiler context, in FunctionExpression node, in ILGenerator IL)
        {
            if (node.Name == nameof(JSON)) { return JSON(in context, in node, in IL); }
            else if (node.Name == nameof(TYPEOF)) { return TYPEOF(in context, in node, in IL); }
            else if (node.Name == nameof(UUIDOF)) { return UUIDOF(in context, in node, in IL); }
            else
            {
                return null;
            }
        }

        private static readonly MethodInfo _toJson = typeof(PublicFunctions)
            .GetMethod(nameof(PublicFunctions.ToJson),
            BindingFlags.Static | BindingFlags.Public, [typeof(object)]);

        private static Type JSON(in ExpressionCompiler context, in FunctionExpression node, in ILGenerator IL)
        {
            foreach (SyntaxNode parameter in node.Parameters)
            {
                Type type = context.Evaluate(in parameter, in IL); // push parameter value onto stack
            }

            IL.Emit(OpCodes.Call, _toJson); // push return value onto stack

            return typeof(string);
        }

        private static readonly PropertyInfo EntityTypeCode = typeof(Entity)
            .GetProperty(nameof(Entity.TypeCode), BindingFlags.Instance | BindingFlags.Public);

        private static Type TYPEOF(in ExpressionCompiler context, in FunctionExpression node, in ILGenerator IL)
        {
            SyntaxNode parameter = node.Parameters[0];

            if (parameter is VariableReference variable)
            {
                if (variable.Binding is DeclareStatement declare && declare.Type.IsEntity)
                {
                    _ = context.Evaluate(in variable, in IL); // push entity value onto stack
                }
            }
            else if (parameter is MemberAccessExpression member)
            {
                _ = context.Evaluate(in member, in IL); // push entity value onto stack
            }

            IL.Emit(OpCodes.Stloc_1); ///<see cref="MsDatabaseMapper.MapInput"/>
            IL.Emit(OpCodes.Ldloca_S, 1); // load address of local variable onto stack
            IL.Emit(OpCodes.Call, EntityTypeCode.GetGetMethod());

            return typeof(int);
        }

        private static readonly PropertyInfo EntityIdentity = typeof(Entity)
            .GetProperty(nameof(Entity.Identity), BindingFlags.Instance | BindingFlags.Public);

        private static Type UUIDOF(in ExpressionCompiler context, in FunctionExpression node, in ILGenerator IL)
        {
            SyntaxNode parameter = node.Parameters[0];

            if (parameter is VariableReference variable)
            {
                if (variable.Binding is DeclareStatement declare && declare.Type.IsEntity)
                {
                    _ = context.Evaluate(in variable, in IL); // push entity value onto stack
                }
            }
            else if (parameter is MemberAccessExpression member)
            {
                _ = context.Evaluate(in member, in IL); // push entity value onto stack
            }

            IL.Emit(OpCodes.Stloc_1); ///<see cref="MsDatabaseMapper.MapInput"/>
            IL.Emit(OpCodes.Ldloca_S, 1); // load address of local variable onto stack
            IL.Emit(OpCodes.Call, EntityIdentity.GetGetMethod());

            return typeof(Guid);
        }
    }
}