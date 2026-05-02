using DaJet.Scripting.Model;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace DaJet.Scripting
{
    public static class SchemaRegistry
    {
        private readonly static string ASSEMBLY_NAME = "SchemaRegistry";
        private readonly static AssemblyBuilder ASSEMBLY;
        private readonly static ModuleBuilder MODULE;
        static SchemaRegistry()
        {
            AssemblyName name = new(ASSEMBLY_NAME);

            AssemblyBuilderAccess access = AssemblyBuilderAccess.Run;

            ASSEMBLY = AssemblyBuilder.DefineDynamicAssembly(name, access);

            MODULE = ASSEMBLY.DefineDynamicModule(ASSEMBLY_NAME);
        }
        public static Type[] GetTypes()
        {
            return ASSEMBLY.GetTypes();
        }
        public static bool TryGet(in string fullName, out Type type)
        {
            type = ASSEMBLY.GetType(fullName);

            return type is not null;
        }

        public static bool TryRegister(in List<DefineStatement> definitions, out string error)
        {
            error = null;

            try
            {
                Register(in definitions);
            }
            catch (Exception exception)
            {
                error = ExceptionHelper.GetErrorMessage(exception);
            }

            return error is null;
        }
        private static void Register(in List<DefineStatement> definitions)
        {
            ArgumentNullException.ThrowIfNull(definitions, nameof(definitions));

            if (definitions.Count == 0) { return; }

            string typeName;
            TypeBuilder builder;
            Dictionary<string, TypeBuilder> candidates = new();

            foreach (DefineStatement definition in definitions)
            {
                typeName = definition.Identifier;

                if (TryGet(typeName, out _))
                {
                    continue; // Produce warning ? The type definition already exists.
                }

                if (candidates.ContainsKey(typeName))
                {
                    continue; // Produce warning ? Duplicate type definition detected.
                }

                builder = MODULE.DefineType(typeName, TypeAttributes.Public);

                candidates.Add(typeName, builder);
            }

            if (candidates.Count == 0)
            {
                return; // Nothing to compile and register
            }

            foreach (DefineStatement definition in definitions)
            {
                typeName = definition.Identifier;

                if (TryGet(typeName, out _))
                {
                    continue;
                }

                CompileType(in definition, in candidates);
            }
        }
        private static void CompileType(in DefineStatement definition, in Dictionary<string, TypeBuilder> candidates)
        {
            string typeName = definition.Identifier;

            if (!candidates.TryGetValue(typeName, out TypeBuilder builder))
            {
                throw new InvalidOperationException($"Type definition [{typeName}] is not found!");
            }

            Type propertyType;

            foreach (DefineProperty property in definition.Properties)
            {
                if (property.Type.IsObject || property.Type.IsArray)
                {
                    if (!TryGet(property.Schema, out propertyType))
                    {
                        if (!candidates.TryGetValue(property.Schema, out TypeBuilder type))
                        {
                            throw new InvalidOperationException($"Definition of [{typeName}] is not found!");
                        }

                        propertyType = type;
                    }

                    if (property.Type.IsArray)
                    {
                        propertyType = typeof(List<>).MakeGenericType([propertyType]);
                    }
                }
                else // simple type
                {
                    propertyType = property.Type.MapToType();
                }

                CompileProperty(builder, property.Name, propertyType);
            }

            _ = builder.CreateType();
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

        public static bool TryImport(in string fullPath, out string error)
        {
            error = null;

            try
            {
                List<DefineStatement> definitions = new();

                if (Path.GetExtension(fullPath) == ".djs")
                {
                    ImportFromFile(in fullPath, in definitions);
                }
                else if (Directory.Exists(fullPath))
                {
                    Import(in fullPath, in definitions);
                }
                else
                {
                    error = $"Path not found!"; return false;
                }

                Register(in definitions);
            }
            catch (Exception exception)
            {
                error = ExceptionHelper.GetErrorMessage(exception);
            }

            return error is null;
        }
        private static void Import(in string catalogPath, in List<DefineStatement> definitions)
        {
            foreach (string catalog in Directory.EnumerateDirectories(catalogPath))
            {
                Import(in catalog, in definitions);
            }

            foreach (string file in Directory.EnumerateFiles(catalogPath, "*.djs"))
            {
                ImportFromFile(in file, in definitions);
            }
        }
        private static void ImportFromFile(in string filePath, in List<DefineStatement> definitions)
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            string source;

            using (StreamReader reader = new(filePath, Encoding.UTF8))
            {
                source = reader.ReadToEnd();
            }

            Parser parser = new();

            if (!parser.TryParse(in source, out Script script, out string error))
            {
                throw new FormatException(error);
            }

            foreach (SyntaxNode node in script.Statements)
            {
                if (node is ImportStatement import)
                {
                    Import(import.Source, in definitions);
                }
                else if (node is DefineStatement definition)
                {
                    definitions.Add(definition);
                }
            }
        }
    }
}