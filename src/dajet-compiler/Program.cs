using DaJet.Data;
using DaJet.Json;
using DaJet.Metadata;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace DaJet.Compiler
{
    internal class Runner
    {
        private bool cancel;
        internal string Name { get; set; } = string.Empty;
        internal string Status { get; set; } = string.Empty;
        internal void Execute()
        {
            while (!cancel)
            {
                Console.WriteLine($"[{Environment.CurrentManagedThreadId}] running ...");

                Task.Delay(TimeSpan.FromSeconds(1)).Wait();
            }

            Console.WriteLine($"[{Environment.CurrentManagedThreadId}] canceled.");

            Status = "Canceled";
        }
        internal void Cancel()
        {
            cancel = true;
        }
    }
    internal class Program
    {
        private static readonly string MS_UNF = "Data Source=ZHICHKIN;Initial Catalog=unf;Integrated Security=True;Encrypt=False;";
        private static readonly string MS_TEST = "Data Source=ZHICHKIN;Initial Catalog=dajet-metadata;Integrated Security=True;Encrypt=False;";
        private static readonly string PG_TEST = "Host=localhost;Port=5432;Database=dajet-metadata;Username=postgres;Password=postgres;";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        static void Main(string[] args)
        {
            JsonOptions.Converters.Add(new DataTypeJsonConverter());
            JsonOptions.Converters.Add(new JsonStringEnumConverter());

            //ProcessorBase select = new TestSelectProcessor(null);

            //select.Cancel(); return;

            //Test(); return;
            //TestRunner(); return;

            //TestDeclareStatement();

            //TestScriptBinding();

            TestCompiler();

            //ImportDefinitions();
            //RegisterDefinitions();
        }
        private static void Test()
        {
            string source = "d.zhichkin";
            Console.WriteLine(source);
            string encoded = UrlEncoder.Create(UnicodeRanges.All).Encode(source);
            Console.WriteLine(encoded);
        }
        private static void TestRunner()
        {
            Runner runner1 = new()
            {
                Name = "Runner one"
            };

            Runner runner2 = new()
            {
                Name = "Runner two"
            };

            Task task1 = Task.Factory.StartNew(runner1.Execute);
            Task task2 = Task.Factory.StartNew(runner2.Execute);

            Task.Delay(TimeSpan.FromSeconds(3)).Wait();

            runner1.Cancel();
            runner2.Cancel();

            Task.WaitAll([task1, task2]);

            Console.WriteLine($"Runner 1: {runner1.Status}");
            Console.WriteLine($"Runner 2: {runner2.Status}");

            Console.WriteLine("Runners done");
        }

        private static void TestDeclareStatement()
        {
            List<string> scripts = new()
            {
                "DECLARE @variable boolean",
                "DECLARE @variable decimal",
                "DECLARE @variable decimal(7)",
                "DECLARE @variable decimal(6,2)",
                "DECLARE @variable integer",
                "DECLARE @variable integer(8)",
                "DECLARE @variable datetime",
                "DECLARE @variable date",
                "DECLARE @variable time",
                "DECLARE @variable string",
                "DECLARE @variable string(10)",
                "DECLARE @variable string(10,fixed)",
                "DECLARE @variable binary",
                "DECLARE @variable binary(1)",
                "DECLARE @variable binary(16,fixed)",
                "DECLARE @variable uuid",
                "DECLARE @variable entity",
                "DECLARE @variable object",
                "DECLARE @variable array",
                "DECLARE @variable union(entity)",
                "DECLARE @variable union(string)",
                "DECLARE @variable union(boolean, decimal(6,4), datetime, string(25))",
                "DECLARE @variable union(boolean, decimal, datetime, string, entity)"
            };

            foreach (string script in scripts)
            {
                Console.WriteLine(script);

                Parser parser = new();

                if (!parser.TryParse(in script, out Script syntaxTree, out string error))
                {
                    Console.WriteLine(error);
                    Console.WriteLine("---------------------");
                    continue;
                }

                if (syntaxTree.Statements[0] is not DeclareStatement declare)
                {
                    Console.WriteLine("Чёта пошло не так ...");
                    Console.WriteLine("---------------------");
                    continue;
                }

                Console.WriteLine(declare.Type.ToString());
                Console.WriteLine("---------------------");
            }
        }

        private static void TestScriptBinding()
        {
            string source;
            string filePath = $"{AppContext.BaseDirectory}scripts\\select_simple.djs";

            using (StreamReader reader = new(filePath, Encoding.UTF8))
            {
                source = reader.ReadToEnd();
            }

            Parser parser = new();

            if (!parser.TryParse(in source, out Script script, out string error))
            {
                Console.WriteLine(error); return;
            }

            OneDbSchemaProvider schema = new();
            MetadataProvider.Add("MS_TEST", DataSourceType.SqlServer, in MS_TEST);
            MetadataProvider.Add("PG_TEST", DataSourceType.PostgreSql, in PG_TEST);

            //EntityDefinition entity = schema.GetSchema("MS_UNF", "Справочник.Номенклатура");

            //Guid value = schema.GetEnumerationValue("MS_UNF", "Перечисление.СпособыДоставки.Самовывоз");

            DaJet.Scripting.Binder binder = new();

            if (!binder.TryBind(in script, schema, out List<string> errors))
            {
                Console.WriteLine(string.Join('\n', errors)); return;
            }

            SqlTranspiler transpiler = new("SqlServer", 2000);

            if (!transpiler.TryTranspile(script, out List<SqlStatement> statements, out errors))
            {
                Console.WriteLine(string.Join('\n', errors)); return;
            }

            foreach (SqlStatement statement in statements)
            {
                Console.WriteLine("-----------");
                Console.WriteLine(statement.Sql);
                Console.WriteLine("-----------");

                SyntaxNode parameter;

                for (int p = 0; p < statement.Input.Count; p++)
                {
                    parameter = statement.Input[p];

                    Console.WriteLine($"@p{p} : {parameter}");
                }

                if (statement.Node is SelectStatement)
                {
                    if (statement.Output is VariableReference variable)
                    {
                        if (variable.Binding is DeclareStatement declare)
                        {
                            if (declare.Binding is EntityDefinition entity)
                            {
                                Console.WriteLine("-----------------------------------------");
                                Console.WriteLine($"Schema [{declare.Type}] OF {entity.Name}");

                                foreach (PropertyDefinition property in entity.Properties)
                                {
                                    Console.WriteLine($"- {property.Name} {property.Type}");
                                }

                                Console.WriteLine("-----------------------------------------");
                            }
                        }
                    }
                }
            }

            string json = JsonSerializer.Serialize(script, JsonOptions);

            Console.WriteLine(json);
        }

        private static void ImportDefinitions()
        {
            string catalogPath = $"{AppContext.BaseDirectory}scripts\\imports\\"; // imports.djs // 

            Console.WriteLine(catalogPath);
            Console.WriteLine("---");

            if (!SchemaRegistry.TryImport(in catalogPath, out string error))
            {
                Console.WriteLine(error); return;
            }

            foreach (Type type in SchemaRegistry.GetTypes())
            {
                Console.WriteLine(type.FullName);

                foreach (PropertyInfo property in type.GetProperties())
                {
                    Console.WriteLine($"   + {property.Name} [{property.PropertyType}]");
                }

                Console.WriteLine("---");
            }
        }
        private static void RegisterDefinitions()
        {
            string source;
            string inputPath = $"{AppContext.BaseDirectory}scripts\\definitions.djs";
            string outputFile = "C:\\GitHub\\dajet-scripting\\bld\\definitions.dll";

            Console.WriteLine($"Input: {inputPath}");
            Console.WriteLine("----");

            using (StreamReader reader = new(inputPath, Encoding.UTF8))
            {
                source = reader.ReadToEnd();
            }

            Console.WriteLine(source);
            Console.WriteLine("----");

            Parser parser = new();

            if (!parser.TryParse(in source, out Script script, out string error))
            {
                Console.WriteLine(error); return;
            }

            List<DefineStatement> definitions = new();

            foreach (SyntaxNode node in script.Statements)
            {
                if (node is DefineStatement definition)
                {
                    definitions.Add(definition);
                }
            }

            if (!SchemaRegistry.TryRegister(in definitions, out error))
            {
                Console.WriteLine(error); return;
            }

            foreach (DefineStatement definition in definitions)
            {
                if (SchemaRegistry.TryGet(definition.Identifier, out Type type))
                {
                    Console.WriteLine(type.FullName);

                    foreach (PropertyInfo property in type.GetProperties())
                    {
                        Console.WriteLine($"   + {property.Name} [{property.PropertyType}]");
                    }

                    Console.WriteLine("---");
                }
            }

            //Compiler compiler = new();

            //try
            //{
            //    compiler.Compile(in script, in outputFile);

            //    Console.WriteLine($"Output: {outputFile}");
            //}
            //catch (Exception exception)
            //{
            //    Console.WriteLine($"Output: {exception.Message}");
            //}
        }

        private static void TestCompiler()
        {
            OneDbSchemaProvider schema = new();
            MetadataProvider.Add("MS_UNF", DataSourceType.SqlServer, in MS_UNF);
            MetadataProvider.Add("MS_TEST", DataSourceType.SqlServer, in MS_TEST);

            string source;
            string filePath = $"{AppContext.BaseDirectory}scripts\\select_join_null.djs";

            using (StreamReader reader = new(filePath, Encoding.UTF8))
            {
                source = reader.ReadToEnd();
            }

            Console.WriteLine(source);
            Console.WriteLine("----");

            Parser parser = new();

            if (!parser.TryParse(in source, out Script script, out string error))
            {
                Console.WriteLine(error); return;
            }

            List<DefineStatement> definitions = new();

            foreach (SyntaxNode node in script.Statements)
            {
                if (node is DefineStatement definition)
                {
                    definitions.Add(definition);
                }
            }

            if (!SchemaRegistry.TryRegister(in definitions, out error))
            {
                Console.WriteLine(error); return;
            }

            Scripting.Binder binder = new();

            if (!binder.TryBind(in script, schema, out List<string> errors))
            {
                Console.WriteLine(string.Join('\n', errors)); return;
            }

            SqlTranspiler transpiler = new("SqlServer", 2000);

            if (!transpiler.TryTranspile(script, out List<SqlStatement> statements, out errors))
            {
                Console.WriteLine(string.Join('\n', errors)); return;
            }

            if (statements is not null)
            {
                foreach (SqlStatement statement in statements)
                {
                    Console.WriteLine(statement.Sql);
                    Console.WriteLine("----");
                }
            }

            Compiler compiler = new();

            ScriptProcessor processor = compiler.Compile(in script, in statements);

            if (processor is not null)
            {
                try
                {
                    processor.Execute();

                    Type type = processor.GetType();

                    Console.WriteLine(type);

                    FieldInfo data = type.GetField("_data",
                        BindingFlags.Instance | BindingFlags.NonPublic);

                    type = data.FieldType;

                    object value = data.GetValue(processor);

                    string json = JsonSerializer.Serialize(value, type, JsonOptions);

                    Console.WriteLine(json);
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception.Message);
                    Console.WriteLine("---");
                    Console.WriteLine(exception.StackTrace);
                }
            }
            else
            {
                Console.WriteLine("Compile and save");
            }
        }
        
    }
}