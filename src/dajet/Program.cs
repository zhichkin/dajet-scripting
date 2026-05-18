using DaJet.Data;
using DaJet.Json;
using DaJet.Metadata;
using DaJet.Scripting;
using DaJet.Scripting.Model;
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

            MetadataProvider.Add("MS_UNF", DataSourceType.SqlServer, in MS_UNF);
            MetadataProvider.Add("MS_TEST", DataSourceType.SqlServer, in MS_TEST);
            
            CompileAndRun("benchmark.djs");

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
        private static void CompileAndRun(in string scriptPath)
        {
            string source;
            string filePath = Path.Combine(AppContext.BaseDirectory, "scripts", scriptPath);

            using (StreamReader reader = new(filePath, Encoding.UTF8))
            {
                source = reader.ReadToEnd();
            }

            Console.WriteLine(filePath);
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
            OneDbSchemaProvider schema = new();

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
    }
}