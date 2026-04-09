using DaJet.Scripting.Model;
using Microsoft.Data.SqlClient;
using System.Reflection;
using System.Reflection.Emit;

namespace DaJet.Compiler
{
    public sealed class Compiler
    {
        private readonly Type _parent = typeof(ScriptProcessor);
        public ScriptProcessor Compile(in Script script)
        {
            string assemblyName = "Assembly1";
            AssemblyName name = new(assemblyName);
            AssemblyBuilderAccess access = AssemblyBuilderAccess.RunAndCollect;
            //AssemblyBuilder ab = AssemblyBuilder.DefineDynamicAssembly(name, access);
            PersistedAssemblyBuilder ab = new(name, typeof(object).Assembly);
            ModuleBuilder mb = ab.DefineDynamicModule(assemblyName);
            TypeBuilder tb = mb.DefineType("Script1", TypeAttributes.Public, _parent);

            BuildConfigureMethod(in tb);

            Type type = tb.CreateType();

            ab.Save("C:\\GitHub\\dajet-scripting\\bld\\test.dll"); return null;

            object instance = Activator.CreateInstance(type);

            if (instance is not ScriptProcessor processor)
            {
                throw new InvalidOperationException("Failed to create ScriptProcessor");
            }

            return processor;
        }
        private void BuildConstructor(TypeBuilder builder)
        {
            // Define a default constructor that supplies a default value
            // for the private field. For parameter types, pass the empty
            // array of types or pass null.
            ConstructorBuilder ctor0 = builder.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                Type.EmptyTypes);

            ILGenerator ctor0IL = ctor0.GetILGenerator();
            // For a constructor, argument zero is a reference to the new
            // instance. Push it on the stack before pushing the default
            // value on the stack, then call constructor ctor1.
            ctor0IL.Emit(OpCodes.Ldarg_0);
            //ctor0IL.Emit(OpCodes.Ldc_I4_S, 42);
            //ctor0IL.Emit(OpCodes.Call, ctor1);
            ctor0IL.Emit(OpCodes.Ret);
        }
        private void BuildProperty(TypeBuilder builder, string name, Type type)
        {
            FieldBuilder fbNumber = builder.DefineField(
                $"_{name.ToLowerInvariant()}",
                typeof(int),
                FieldAttributes.Private);

            PropertyBuilder pbNumber = builder.DefineProperty(
            "Number",
            PropertyAttributes.HasDefault,
            typeof(int),
            null);

            // The property "set" and property "get" methods require a special
            // set of attributes.
            MethodAttributes getSetAttr = MethodAttributes.Public |
                MethodAttributes.SpecialName | MethodAttributes.HideBySig;

            // Define the "get" accessor method for Number. The method returns
            // an integer and has no arguments. (Note that null could be
            // used instead of Types.EmptyTypes)
            MethodBuilder mbNumberGetAccessor = builder.DefineMethod(
                "get_Number",
                getSetAttr,
                typeof(int),
                Type.EmptyTypes);

            ILGenerator numberGetIL = mbNumberGetAccessor.GetILGenerator();
            // For an instance property, argument zero is the instance. Load the
            // instance, then load the private field and return, leaving the
            // field value on the stack.
            numberGetIL.Emit(OpCodes.Ldarg_0);
            numberGetIL.Emit(OpCodes.Ldfld, fbNumber);
            numberGetIL.Emit(OpCodes.Ret);

            // Define the "set" accessor method for Number, which has no return
            // type and takes one argument of type int (Int32).
            MethodBuilder mbNumberSetAccessor = builder.DefineMethod(
                "set_Number",
                getSetAttr,
                null,
                new Type[] { typeof(int) });

            ILGenerator numberSetIL = mbNumberSetAccessor.GetILGenerator();
            // Load the instance and then the numeric argument, then store the
            // argument in the field.
            numberSetIL.Emit(OpCodes.Ldarg_0);
            numberSetIL.Emit(OpCodes.Ldarg_1);
            numberSetIL.Emit(OpCodes.Stfld, fbNumber);
            numberSetIL.Emit(OpCodes.Ret);

            // Last, map the "get" and "set" accessor methods to the
            // PropertyBuilder. The property is now complete.
            pbNumber.SetGetMethod(mbNumberGetAccessor);
            pbNumber.SetSetMethod(mbNumberSetAccessor);
        }
        private void BuildMethod(TypeBuilder builder, string name, Type type)
        {
            MethodBuilder method = builder.DefineMethod(
            "MyMethod",
            MethodAttributes.Public,
            typeof(int),
            new Type[] { typeof(int) });

            ILGenerator methIL = method.GetILGenerator();
            // To retrieve the private instance field, load the instance it
            // belongs to (argument zero). After loading the field, load the
            // argument one and then multiply. Return from the method with
            // the return value (the product of the two numbers) on the
            // execution stack.
            methIL.Emit(OpCodes.Ldarg_0);
            //methIL.Emit(OpCodes.Ldfld, fbNumber);
            methIL.Emit(OpCodes.Ldarg_1);
            methIL.Emit(OpCodes.Mul);
            methIL.Emit(OpCodes.Ret);
        }

        private void BuildConfigureMethod(in TypeBuilder builder)
        {
            MethodBuilder method = builder.DefineMethod("Configure",
                MethodAttributes.Family | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                typeof(void), [typeof(SqlCommand)]); // [typeof(SqlCommand).MakeByRefType()]

            MethodInfo get_Parameters = typeof(SqlCommand).GetProperty("Parameters", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).GetGetMethod();
            MethodInfo clear_Parameters = typeof(SqlParameterCollection).GetMethod("Clear", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, Type.EmptyTypes);
            MethodInfo addWithValue = typeof(SqlParameterCollection).GetMethod("AddWithValue", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, [typeof(string), typeof(object)]);

            FieldInfo context = typeof(SelectProcessor).GetField("_context",
                BindingFlags.Instance | BindingFlags.Instance | BindingFlags.NonPublic);

            MethodInfo get_Variable = typeof(ScriptProcessor).GetProperty("Variable",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).GetGetMethod();

            ILGenerator IL = method.GetILGenerator();

            // command.Parameters.Clear();
            IL.Emit(OpCodes.Ldarg_1);
            IL.Emit(OpCodes.Callvirt, get_Parameters);
            IL.Emit(OpCodes.Callvirt, clear_Parameters);

            // command.Parameters.AddWithValue("p0", DBNull.Value);
            IL.Emit(OpCodes.Ldarg_1);
            IL.Emit(OpCodes.Callvirt, get_Parameters);
            IL.Emit(OpCodes.Ldstr, "p0");
            IL.Emit(OpCodes.Ldsfld, typeof(DBNull).GetField("Value"));
            IL.Emit(OpCodes.Callvirt, addWithValue);
            IL.Emit(OpCodes.Pop);

            // command.Parameters.AddWithValue("p6", _context.Variable);
            IL.Emit(OpCodes.Ldarg_1);
            IL.Emit(OpCodes.Callvirt, get_Parameters);
            IL.Emit(OpCodes.Ldstr, "p1");
            IL.Emit(OpCodes.Ldarg_0); // this
            IL.Emit(OpCodes.Ldfld, context);
            IL.Emit(OpCodes.Callvirt, get_Variable);
            IL.Emit(OpCodes.Callvirt, addWithValue);
            IL.Emit(OpCodes.Pop);

            IL.Emit(OpCodes.Ret);
        }
    }
}