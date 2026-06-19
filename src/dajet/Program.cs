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

namespace DaJet.Host
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
        private static readonly string MS_UNF = "Data Source=Z-NOTEBOOK;Initial Catalog=unf;Integrated Security=True;Encrypt=False;";
        private static readonly string PG_UNF = "Host=127.0.0.1;Port=5432;Database=unf;Username=postgres;Password=postgres;";
        private static readonly string MS_TEST = "Data Source=Z-NOTEBOOK;Initial Catalog=test;Integrated Security=True;Encrypt=False;";
        private static readonly string PG_TEST = "Host=localhost;Port=5432;Database=test;Username=postgres;Password=postgres;";

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
            JsonOptions.Converters.Add(new DictionaryJsonConverter());

            MetadataProvider.Add("MS_UNF", DataSourceType.SqlServer, in MS_UNF);
            MetadataProvider.Add("PG_UNF", DataSourceType.PostgreSql, in PG_UNF);
            MetadataProvider.Add("MS_TEST", DataSourceType.SqlServer, in MS_TEST);
            MetadataProvider.Add("PG_TEST", DataSourceType.PostgreSql, in PG_TEST);

            string source;
            string scriptPath = "scalar\\select.djs";
            string filePath = Path.Combine(AppContext.BaseDirectory, "scripts", scriptPath);

            using (StreamReader reader = new(filePath, Encoding.UTF8))
            {
                source = reader.ReadToEnd();
            }

            Console.WriteLine(filePath);
            Console.WriteLine(source);
            Console.WriteLine("----");

            ExecuteQuery(in source);

            //Transpile(in source);
            
            //CompileAndRun(in source);

            //for (int i = 0; i < 1000; i++)
            //{
            //    ProfileCompiler(in source);
            //}

            //ExecuteRunner();
        }
        private static void ExecuteRunner()
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
        private static void Transpile(in string source)
        {
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
            OneDbSchemaProvider schema = new();

            if (!binder.TryBind(in script, schema, out List<string> errors))
            {
                Console.WriteLine(string.Join('\n', errors)); return;
            }

            Transpiler transpiler = new();

            if (!transpiler.TryTranspile(in script, out errors))
            {
                Console.WriteLine(string.Join('\n', errors)); return;
            }

            foreach (SqlStatement statement in script.GetSqlStatements())
            {
                Console.WriteLine(statement.Sql);
                Console.WriteLine("----");
            }
        }
        private static void CompileAndRun(in string source)
        {
            Compiler compiler = new();

            ScriptProcessor processor = compiler.Compile(in source);

            if (processor is not null)
            {
                try
                {
                    processor.Execute();
                    //processor.Execute();
                    //processor.Execute();

                    Type type = processor.GetType();

                    Console.WriteLine(type);

                    FieldInfo data = type.GetField("_data",
                        BindingFlags.Instance | BindingFlags.NonPublic);

                    type = data.FieldType;

                    object value = data.GetValue(processor);

                    string json = JsonSerializer.Serialize(value, type, JsonOptions);

                    Console.WriteLine(json);

                    ShowReturnValue(processor.ReturnValue);
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
        private static void ShowReturnValue(in object value)
        {
            Console.WriteLine($"RETURN = {value ?? "NULL"}");
        }
        private static void ProfileCompiler(in string source)
        {
            Compiler compiler = new();

            ScriptProcessor processor = compiler.Compile(in source);
        }

        private static Dictionary<string, object> GetParametersFromJson()
        {
            string filePath = Path.Combine(AppContext.BaseDirectory, "scripts", "parameters.json");

            using (StreamReader reader = new(filePath, Encoding.UTF8))
            {
                string json = reader.ReadToEnd();

                return JsonSerializer.Deserialize<Dictionary<string, object>>(json, JsonOptions);
            }
        }
        private static void ExecuteQuery(in string source)
        {
            //Dictionary<string, object> parameters = new()
            //{
            //    { "Булево", true },
            //    { "ЦелоеЧисло", 12345 },
            //    { "БольшоеЧисло", 12345L },
            //    { "ДесятичноеЧисло", 12.34M },
            //    { "ДатаВремя", DateTime.Now },
            //    { "Строка", "000000002" },
            //    { "ДвоичноеЧисло", Convert.FromBase64String("DEADBEEF") },
            //    { "Идентификатор", new Guid("41F517C5-BC81-45E6-A9E8-7A2C8F573117") },
            //    { "ПустаяСсылка", Entity.Undefined }
            //};

            //parameters = GetParametersFromJson();

            Dictionary<string, object> parameters = new();

            Script script = new ScriptBuilder().FromSource(in source).Build();

            Interpreter interpreter = new(in script);

            object value = interpreter.Execute(in parameters);

            string json;

            if (value is null)
            {
                json = "Value is NULL";
            }
            else if (value is string text)
            {
                json = text;
            }
            else
            {
                json = JsonSerializer.Serialize(value, value.GetType(), JsonOptions);
            }

            Console.WriteLine(json);
        }
    }
}