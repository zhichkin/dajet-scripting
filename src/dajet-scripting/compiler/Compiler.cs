using DaJet.Data;
using DaJet.Metadata;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using Microsoft.Data.SqlClient;
using System.Reflection;
using System.Reflection.Emit;

namespace DaJet.Scripting
{
    public sealed class Compiler
    {
        private Type ScriptProcessorBase { get; } = typeof(ScriptProcessor);
        private Type SelectProcessorBase { get; } = typeof(SelectProcessor);

        private Dictionary<SyntaxNode, SqlStatement> _statements = new();

        private bool CompileAndSave = false;
        public ScriptProcessor Compile(in Script script, in List<SqlStatement> statements)
        {
            foreach (SqlStatement statement in statements)
            {
                _statements.Add(statement.Node, statement);
            }

            string assemblyName = "Assembly1";
            AssemblyName name = new(assemblyName);
            AssemblyBuilderAccess access = AssemblyBuilderAccess.RunAndCollect;
            
            AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(name, access);
            //CompileAndSave = true;
            //PersistedAssemblyBuilder assembly = new(name, typeof(object).Assembly);
            
            ModuleBuilder module = assembly.DefineDynamicModule(assemblyName);
            ScriptModule = module;

            Type type = BuildScriptProcessor(in script, in module);

            //assembly.Save("C:\\GitHub\\dajet-scripting\\bld\\test.dll"); return null;

            object instance = Activator.CreateInstance(type);

            if (instance is not ScriptProcessor processor)
            {
                throw new InvalidOperationException("Failed to create ScriptProcessor");
            }

            return processor;
        }

        private ModuleBuilder ScriptModule;
        private TypeInfo NewScriptProcessor;
        private ILGenerator Constructor;
        private FieldInfo ScriptReturnValue;
        private FieldInfo ScriptDataField;
        private FieldInfo ScriptProcessorsField;
        private MethodInfo ScriptProcessorsAdd;
        private MethodInfo ScriptProcessorsGetItem;
        private Dictionary<string, PropertyInfo> ScriptData = new();
        
        private readonly Stack<MetadataProvider> ScriptUse = new();
        private Type BuildScriptProcessor(in Script source, in ModuleBuilder module)
        {
            TypeBuilder script = module.DefineType("Script1", TypeAttributes.Public, ScriptProcessorBase);

            NewScriptProcessor = script;

            ScriptReturnValue = typeof(ScriptProcessor).GetField("_returnValue",
                BindingFlags.Instance | BindingFlags.NonPublic);

            ScriptProcessorsField = typeof(ScriptProcessor).GetField("_processors",
                BindingFlags.Instance | BindingFlags.NonPublic);

            ScriptProcessorsAdd = ScriptProcessorsField.FieldType.GetMethod(nameof(List<>.Add),
                BindingFlags.Instance | BindingFlags.Public, [typeof(ProcessorBase)]);

            ScriptProcessorsGetItem = ScriptProcessorsField.FieldType.GetMethod("get_Item",
                BindingFlags.Instance | BindingFlags.Public, [typeof(int)]);

            ScriptDataField = BuildScriptData(in source, in script);

            BuildScriptConstructor(in source, in script);

            ScriptProcessor_Execute(in source, in script);

            Constructor.Emit(OpCodes.Ret);

            return script.CreateType();
        }
        private FieldBuilder BuildScriptData(in Script source, in TypeBuilder script)
        {
            TypeBuilder data = ScriptModule.DefineType("ScriptData", TypeAttributes.Public | TypeAttributes.Sealed);

            foreach (SyntaxNode node in source.Statements)
            {
                if (node is DeclareStatement variable)
                {
                    PropertyInfo property = BuildScriptProperty(in variable, in data);

                    if (property is not null)
                    {
                        ScriptData.Add(variable.Identifier, property);
                    }
                }
            }

            Type type = data.CreateType();

            return script.DefineField("_data", type, FieldAttributes.Assembly);
        }
        private void BuildScriptConstructor(in Script source, in TypeBuilder script)
        {
            ConstructorInfo baseCtor = ScriptProcessorBase.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic, Type.EmptyTypes);

            ConstructorBuilder thisCtor = script.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                Type.EmptyTypes);

            ConstructorInfo dataCtor = ScriptDataField.FieldType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public, Type.EmptyTypes);

            Constructor = thisCtor.GetILGenerator();

            ILGenerator IL = Constructor;

            // call base class constructor
            IL.Emit(OpCodes.Ldarg_0);
            IL.Emit(OpCodes.Call, baseCtor);

            // this._data = new ScriptData();
            IL.Emit(OpCodes.Ldarg_0); // this
            IL.Emit(OpCodes.Newobj, dataCtor); // value
            IL.Emit(OpCodes.Stfld, ScriptDataField);

            ExpressionCompiler expression = new(ScriptDataField, ScriptData);

            foreach (SyntaxNode node in source.Statements)
            {
                if (node is DeclareStatement variable)
                {
                    if (ScriptData.TryGetValue(variable.Identifier, out PropertyInfo property))
                    {
                        if (variable.Initializer is ScalarExpression scalar)
                        {
                            //if (scalar.Token == Token.String)
                            //{
                            //    // this.Свойство = "ЭтоСтрока";
                            //    IL.Emit(OpCodes.Ldarg_0);
                            //    IL.Emit(OpCodes.Ldstr, scalar.Literal);
                            //    IL.Emit(OpCodes.Callvirt, property.GetSetMethod());
                            //}

                            IL.Emit(OpCodes.Ldarg_0); // this
                            IL.Emit(OpCodes.Ldfld, ScriptDataField); // _data

                            Type value = expression.Evaluate(variable.Initializer, in IL);
                            
                            IL.Emit(OpCodes.Call, property.GetSetMethod());
                        }
                        else
                        {
                            if (variable.Initializer is null)
                            {
                                if (variable.Type.IsObject) // Type1
                                {
                                    ConstructorInfo _ctor = property.PropertyType.GetConstructor(
                                        BindingFlags.Instance | BindingFlags.Public, Type.EmptyTypes);

                                    // this.Свойство = new Type1();
                                    IL.Emit(OpCodes.Ldarg_0);
                                    IL.Emit(OpCodes.Ldfld, ScriptDataField); // _data
                                    IL.Emit(OpCodes.Newobj, _ctor);
                                    IL.Emit(OpCodes.Call, property.GetSetMethod());
                                }
                                else if (variable.Type.IsArray) // List<Type1>
                                {
                                    ConstructorInfo _ctor;

                                    if (CompileAndSave) // Режим записи библиотеки на диск
                                    {
                                        _ctor = TypeBuilder.GetConstructor(property.PropertyType,
                                            typeof(List<>).GetConstructor(Type.EmptyTypes));
                                    }
                                    else
                                    {
                                        _ctor = property.PropertyType.GetConstructor(
                                            BindingFlags.Instance | BindingFlags.Public, Type.EmptyTypes);
                                    }

                                    // this.Свойство = new List<Type1>();
                                    IL.Emit(OpCodes.Ldarg_0);
                                    IL.Emit(OpCodes.Ldfld, ScriptDataField); // _data
                                    IL.Emit(OpCodes.Newobj, _ctor);
                                    IL.Emit(OpCodes.Call, property.GetSetMethod());
                                }
                            }
                        }
                    }
                }
            }

            //IL.Emit(OpCodes.Ret);
        }
        private void InitializeScriptData()
        {

        }
        
        private PropertyInfo BuildProperty(TypeBuilder builder, string name, Type type)
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

            return property;
        }
        private Type GetOrBuildType(in DefineStatement schema)
        {
            string typeName = "AnonymousDataSchema." + schema.Identifier.TrimStart('@');

            TypeBuilder type = ScriptModule.DefineType(typeName, TypeAttributes.Public);

            foreach (DefineProperty property in schema.Properties)
            {
                if (property.Type.IsObject || property.Type.IsArray)
                {
                    //TODO: get type from schema name
                }

                Type propertyType = property.Type.MapToType();

                _ = BuildProperty(type, property.Name, propertyType);
            }

            return type.CreateType();
        }
        private PropertyInfo BuildScriptProperty(in DeclareStatement variable, in TypeBuilder script)
        {
            string propertyName = variable.Identifier.TrimStart('@');

            Type propertyType = null;

            if (variable.Type.IsObject || variable.Type.IsArray)
            {
                if (!string.IsNullOrEmpty(variable.Schema))
                {
                    if (!SchemaRegistry.TryGet(variable.Schema, out propertyType))
                    {
                        throw new InvalidOperationException($"Definition of [{variable.Schema}] is not found!");
                    }

                    if (variable.Type.IsArray)
                    {
                        propertyType = typeof(List<>).MakeGenericType([propertyType]);
                    }
                }
                else if (variable.Binding is not null)
                {
                    propertyType = GetOrBuildType(variable.Binding);

                    if (variable.Type.IsArray)
                    {
                        propertyType = typeof(List<>).MakeGenericType([propertyType]);
                    }
                }
            }
            else
            {
                propertyType = variable.Type.MapToType();
            }

            if (propertyType is null)
            {
                return null; //TODO: throw !?
            }

            return BuildProperty(script, propertyName, propertyType);
        }

        private int _counter;
        private void ScriptProcessor_Execute(in Script source, in TypeBuilder script)
        {
            _counter = 0;

            MethodAttributes attributes = MethodAttributes.Family
                | MethodAttributes.Virtual
                | MethodAttributes.HideBySig;

            MethodBuilder method = script.DefineMethod("Process", attributes, typeof(void), Type.EmptyTypes);
            
            ILGenerator IL = method.GetILGenerator();

            foreach (SyntaxNode node in source.Statements)
            {
                Compile(in node, in IL);
            }

            if (source.Statements[source.Statements.Count - 1] is not ReturnStatement)
            {
                IL.Emit(OpCodes.Ret);
            }
        }

        private void Compile(in SyntaxNode node, in ILGenerator IL)
        {
            if (node is PrintStatement print) { Compile(in print, in IL); }
            else if (node is UseStatement use) { Compile(in use, in IL); }
            else if (node is SelectStatement select) { Compile(in select, in IL); }
            else if (node is ReturnStatement _return) { Compile(in _return, in IL); }
        }
        private void Compile(in PrintStatement statement, in ILGenerator IL)
        {
            ExpressionCompiler expression = new(ScriptDataField, ScriptData);

            Type value = expression.Evaluate(statement.Expression, in IL);

            IL.Emit(OpCodes.Call, typeof(Console).GetMethod(nameof(Console.WriteLine),
                BindingFlags.Static | BindingFlags.Public, [typeof(string)]));
        }
        private void Compile(in ReturnStatement statement, in ILGenerator IL)
        {
            ExpressionCompiler expression = new(ScriptDataField, ScriptData);

            IL.Emit(OpCodes.Ldarg_0); // this ScriptProcessor

            Type value = null;

            if (statement.Expression is not null)
            {
                value = expression.Evaluate(statement.Expression, in IL);
            }

            if (value is null)
            {
                IL.Emit(OpCodes.Ldnull);
            }
            else if (value.IsValueType)
            {
                IL.Emit(OpCodes.Box, value);
            }

            IL.Emit(OpCodes.Stfld, ScriptReturnValue); // _returnValue

            IL.Emit(OpCodes.Ret); //THINK: flag existence of the RETURN statement !?
        }
        private void Compile(in UseStatement statement, in ILGenerator IL)
        {
            MetadataProvider provider = MetadataProvider.Get(statement.Uri);
            
            ScriptUse.Push(provider);

            IL.Emit(OpCodes.Ldarg_0); // this ScriptProcessor
            IL.Emit(OpCodes.Ldstr, provider.ConnectionString);
            IL.Emit(OpCodes.Call, typeof(ScriptProcessor).GetMethod("UseDataSource",
                BindingFlags.Instance | BindingFlags.NonPublic, [typeof(string)]));
            
            _ = IL.BeginExceptionBlock();

            foreach (SyntaxNode node in statement.Statements.Statements)
            {
                Compile(in node, in IL);
            }

            IL.BeginFinallyBlock();

            IL.Emit(OpCodes.Ldarg_0); // this ScriptProcessor
            IL.Emit(OpCodes.Call, typeof(ScriptProcessor).GetMethod("DisposeDataSource",
                BindingFlags.Instance | BindingFlags.NonPublic, Type.EmptyTypes));

            IL.EndExceptionBlock();

            _ = ScriptUse.Pop();
        }
        private void Compile(in SelectStatement statement, in ILGenerator IL)
        {
            if (!_statements.TryGetValue(statement, out SqlStatement sql))
            {
                return;
            }

            EntityDefinition schema = DataMapper.InferEntity(in statement);

            _counter++;

            TypeBuilder type = ScriptModule.DefineType($"Select{_counter}",
                TypeAttributes.Public, SelectProcessorBase);

            MethodAttributes attributes = MethodAttributes.Public
                | MethodAttributes.Virtual
                | MethodAttributes.HideBySig;

            // Ссылка на родительский ScriptProcessor
            FieldInfo _data = type.DefineField("_data", ScriptDataField.FieldType,
                FieldAttributes.Private | FieldAttributes.InitOnly);

            //MetadataProvider use = ScriptUse.Peek();
            MethodInfo set_SqlCommand = typeof(SelectProcessor)
                .GetProperty("SqlCommand", BindingFlags.Instance | BindingFlags.Public)
                .GetSetMethod();
            
            MethodBuilder initializer = type.DefineMethod("Initialize", attributes, typeof(void), Type.EmptyTypes);
            ILGenerator initIL = initializer.GetILGenerator();
            initIL.Emit(OpCodes.Ldarg_0);
            initIL.Emit(OpCodes.Ldstr, sql.Sql);
            initIL.Emit(OpCodes.Call, set_SqlCommand);
            initIL.Emit(OpCodes.Ret);

            ConstructorInfo ctor = BuildSelectProcessorConstructor(in type, in _data, initializer);

            SelectProcessor_Configure(in type, sql.Input, in _data);

            if (sql.Output is VariableReference variable)
            {
                if (ScriptData.TryGetValue(variable.Identifier, out PropertyInfo output))
                {
                    SelectProcessor_Process(in type, in schema, in _data, in output);
                }
            }

            Type processor = type.CreateType();

            // this._processors.Add(new Select1(this));
            Constructor.Emit(OpCodes.Ldarg_0);
            Constructor.Emit(OpCodes.Ldfld, ScriptProcessorsField);
            Constructor.Emit(OpCodes.Ldarg_0);
            Constructor.Emit(OpCodes.Newobj, ctor);
            Constructor.Emit(OpCodes.Callvirt, ScriptProcessorsAdd);

            MethodInfo execute = processor.GetMethod(nameof(ProcessorBase.Execute),
                BindingFlags.Instance | BindingFlags.Public, Type.EmptyTypes);

            // this._processors[0].Execute();
            int index = _counter - 1; //TODO: fix this !!!
            IL.Emit(OpCodes.Ldarg_0);
            IL.Emit(OpCodes.Ldfld, ScriptProcessorsField);
            IL.Emit(OpCodes.Ldc_I4, index);
            IL.Emit(OpCodes.Callvirt, ScriptProcessorsGetItem);
            //IL.Emit(OpCodes.Isinst, processor); !?
            IL.Emit(OpCodes.Callvirt, execute);
        }
        private ConstructorInfo BuildSelectProcessorConstructor(in TypeBuilder type, in FieldInfo data, in MethodInfo initializer)
        {
            ConstructorInfo ctor = SelectProcessorBase.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic, [ScriptProcessorBase]);

            ConstructorBuilder builder = type.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                [NewScriptProcessor]);

            ILGenerator IL = builder.GetILGenerator();

            // call base class constructor : base(ScriptProcessor)
            IL.Emit(OpCodes.Ldarg_0); // this SelectProcessor
            IL.Emit(OpCodes.Ldarg_1); // parameter ScriptProcessor
            IL.Emit(OpCodes.Call, ctor); // base class constructor

            // this._data = script._data;
            IL.Emit(OpCodes.Ldarg_0); // this SelectProcessor
            IL.Emit(OpCodes.Ldarg_1); // parameter ScriptProcessor
            IL.Emit(OpCodes.Ldfld, ScriptDataField); // script._data
            IL.Emit(OpCodes.Stfld, data); // this._data = script._data

            IL.Emit(OpCodes.Ldarg_0);
            IL.Emit(OpCodes.Callvirt, initializer);

            IL.Emit(OpCodes.Ret);

            return builder;
        }
        private void SelectProcessor_Configure(in TypeBuilder type, in List<SyntaxNode> input, in FieldInfo data)
        {
            MethodAttributes attributes = MethodAttributes.Public
                | MethodAttributes.Virtual
                | MethodAttributes.HideBySig;

            MethodBuilder method = type.DefineMethod("Configure", attributes, typeof(void), [typeof(SqlCommand)]);

            ILGenerator IL = method.GetILGenerator();

            MetadataProvider provider = ScriptUse.Peek();
            if (provider.DataSource == DataSourceType.SqlServer)
            {
                MsDataMapper.YearOffset = provider.GetYearOffset();
            }

            if (input is not null && input.Count > 0)
            {
                MsDataMapper.MapInput(in input, in data, ScriptData, in IL);
            }

            IL.Emit(OpCodes.Ret);
        }
        private void SelectProcessor_Process(in TypeBuilder type, in EntityDefinition schema, in FieldInfo data, in PropertyInfo output)
        {
            Type outputType = output.PropertyType;

            bool isArray = outputType.IsGenericList();

            if (isArray)
            {
                outputType = outputType.GetGenericArguments()[0];
            }

            MethodAttributes attributes = MethodAttributes.Public
                | MethodAttributes.Virtual
                | MethodAttributes.HideBySig;

            MethodBuilder method = type.DefineMethod("Process", attributes, typeof(void), [typeof(SqlDataReader)]);

            ILGenerator IL = method.GetILGenerator();
            
            _ = IL.DeclareLocal(outputType); // Loc_0
            _ = IL.DeclareLocal(typeof(byte[])); // Loc_1
            _ = IL.DeclareLocal(typeof(DateTime)); // Loc_2

            if (isArray) // array
            {
                ConstructorInfo ctor = outputType.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public, Type.EmptyTypes);

                // OutputType record = new OutputType();
                
                IL.Emit(OpCodes.Newobj, ctor);
                IL.Emit(OpCodes.Stloc_0);
            }
            else // object
            {
                // OutputType record = _context.OutputProperty;

                IL.Emit(OpCodes.Ldarg_0); // this SelectProcessor
                IL.Emit(OpCodes.Ldfld, data); // script data field
                IL.Emit(OpCodes.Call, output.GetGetMethod());
                IL.Emit(OpCodes.Stloc_0);
            }

            // byte[] buffer = new byte[16];
            IL.Emit(OpCodes.Ldc_I4, 16);
            IL.Emit(OpCodes.Newarr, typeof(byte));
            IL.Emit(OpCodes.Stloc_1);

            // record.Ссылка = new Entity(123, new Guid(buffer));

            ///<see cref="SelectProcessor_Configure"/>
            //MetadataProvider provider = ScriptUse.Peek();
            //if (provider.DataSource == DataSourceType.SqlServer)
            //{
            //    MsDataMapper.YearOffset = provider.GetYearOffset();
            //}

            MsDataMapper.MapOutput(in outputType, in schema, in IL);

            // _context.OutputProperty.Add(record);

            if (isArray)
            {
                MethodInfo ListAdd = CompileAndSave
                    ? typeof(List<>).GetMethod(nameof(List<>.Add),
                    BindingFlags.Instance | BindingFlags.Public)
                    : output.PropertyType.GetMethod(nameof(List<>.Add),
                    BindingFlags.Instance | BindingFlags.Public, [outputType]);

                IL.Emit(OpCodes.Ldarg_0); // this SelectProcessor
                IL.Emit(OpCodes.Ldfld, data); // _data
                IL.Emit(OpCodes.Call, output.GetGetMethod());
                IL.Emit(OpCodes.Ldloc_0); // OutputType record

                if (CompileAndSave) // Режим записи библиотеки на диск
                {
                    IL.Emit(OpCodes.Callvirt, TypeBuilder.GetMethod(output.PropertyType, ListAdd));
                }
                else
                {
                    IL.Emit(OpCodes.Callvirt, ListAdd);
                }
            }

            IL.Emit(OpCodes.Ret);
        }
    }
}