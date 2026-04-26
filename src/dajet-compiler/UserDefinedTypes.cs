using DaJet.Scripting.Model;
using System.Reflection;
using System.Reflection.Emit;

namespace DaJet.Compiler
{
    internal class TypeToCompile
    {
        internal TypeBuilder Builder { get; set; }
        internal DefineStatement Definition { get; set; }
    }
    internal static class UserDefinedTypes
    {
        private readonly static string ASSEMBLY_NAME = "UserDefinedTypes";
        private readonly static AssemblyBuilder ASSEMBLY;
        private readonly static ModuleBuilder MODULE;
        static UserDefinedTypes()
        {
            AssemblyName name = new(ASSEMBLY_NAME);
            
            AssemblyBuilderAccess access = AssemblyBuilderAccess.Run;
            
            ASSEMBLY = AssemblyBuilder.DefineDynamicAssembly(name, access);
            
            MODULE = ASSEMBLY.DefineDynamicModule(ASSEMBLY_NAME);
        }
        internal static bool TryGet(in string name, out Type type)
        {
            type = ASSEMBLY.GetType(name);

            return type is not null;
        }
        
        internal static void Register(in List<DefineStatement> definitions)
        {
            Dictionary<string, TypeToCompile> types = new();

            foreach (DefineStatement definition in definitions)
            {
                if (TryGet(definition.Identifier, out _))
                {
                    continue;
                }

                TypeBuilder builder = MODULE.DefineType(definition.Identifier, TypeAttributes.Public);

                TypeToCompile item = new()
                {
                    Builder = builder,
                    Definition = definition
                };

                types.Add(definition.Identifier, item);
            }

            foreach (DefineStatement definition in definitions)
            {
                if (TryGet(definition.Identifier, out _))
                {
                    continue;
                }

                _ = CompileTypeDefinition(definition.Identifier, in types);
            }
        }
        private static Type CompileTypeDefinition(in string typeName, in Dictionary<string, TypeToCompile> types)
        {
            if (!types.TryGetValue(typeName, out TypeToCompile type))
            {
                throw new InvalidOperationException($"Definition of [{typeName}] is not found!");
            }

            foreach (DefineProperty property in type.Definition.Properties)
            {
                Type propertyType = property.Type.MapToType();

                if (property.Type.IsObject || property.Type.IsArray)
                {
                    if (!TryGet(property.Schema, out propertyType))
                    {
                        if (!types.TryGetValue(property.Schema, out TypeToCompile item))
                        {
                            throw new InvalidOperationException($"Definition of [{typeName}] is not found!");
                        }

                        propertyType = item.Builder;
                    }
                }

                if (property.Type.IsArray)
                {
                    propertyType = typeof(List<>).MakeGenericType([propertyType]);
                }

                CompileProperty(type.Builder, property.Name, propertyType);
            }

            return type.Builder.CreateType();
        }
        private static void CompileProperty(TypeBuilder builder, string name, Type type)
        {
            MethodAttributes getSetAttr = MethodAttributes.Public
                | MethodAttributes.SpecialName
                | MethodAttributes.HideBySig;

            FieldBuilder field = builder.DefineField($"_{name}", type, FieldAttributes.Private);

            PropertyBuilder property = builder.DefineProperty(name, PropertyAttributes.None, type, null);

            MethodBuilder getAccessor = builder.DefineMethod($"get_{name}", getSetAttr, type, Type.EmptyTypes);
            ILGenerator getIL = getAccessor.GetILGenerator();
            getIL.Emit(OpCodes.Ldarg_0); // this
            getIL.Emit(OpCodes.Ldfld, field);
            getIL.Emit(OpCodes.Ret);
            property.SetGetMethod(getAccessor);

            MethodBuilder setAccessor = builder.DefineMethod($"set_{name}", getSetAttr, null, [type]);
            ILGenerator setIL = setAccessor.GetILGenerator();
            setIL.Emit(OpCodes.Ldarg_0); // this
            setIL.Emit(OpCodes.Ldarg_1); // value
            setIL.Emit(OpCodes.Stfld, field);
            setIL.Emit(OpCodes.Ret);
            property.SetSetMethod(setAccessor);
        }

        //private static void ImportTypeDefinitions(in string catalogPath, in Dictionary<string, DefineStatement> definitions)
        //{
        //    if (!Directory.Exists(catalogPath))
        //    {
        //        return;
        //    }

        //    foreach (string file in Directory.EnumerateFiles(catalogPath, "*.djs"))
        //    {
        //        ImportTypeDefinitionsFromFile(in file, in definitions);
        //    }

        //    foreach (string catalog in Directory.EnumerateDirectories(catalogPath))
        //    {
        //        ImportTypeDefinitions(in catalog, definitions);
        //    }
        //}
        //private static void ImportTypeDefinitionsFromFile(in string filePath, in Dictionary<string, DefineStatement> definitions)
        //{
        //    if (!File.Exists(filePath))
        //    {
        //        return;
        //    }

        //    string source;

        //    using (StreamReader reader = new(filePath, Encoding.UTF8))
        //    {
        //        source = reader.ReadToEnd();
        //    }

        //    Parser parser = new();

        //    if (!parser.TryParse(in source, out Script script, out string error))
        //    {
        //        throw new FormatException(error);
        //    }

        //    foreach (SyntaxNode node in script.Statements)
        //    {
        //        if (node is DefineStatement definition)
        //        {
        //            definitions.Add(definition.Identifier, definition);
        //        }
        //    }
        //}
    }
}