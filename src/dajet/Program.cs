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
using System.Text.Json.Nodes;
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
            JsonOptions.Converters.Add(new EntityJsonConverter());
            JsonOptions.Converters.Add(new DataTypeJsonConverter());
            JsonOptions.Converters.Add(new DataObjectJsonConverter());
            JsonOptions.Converters.Add(new JsonStringEnumConverter());
            
            MetadataProvider.Add("MS_UNF", DataSourceType.SqlServer, in MS_UNF);
            MetadataProvider.Add("PG_UNF", DataSourceType.PostgreSql, in PG_UNF);
            MetadataProvider.Add("MS_TEST", DataSourceType.SqlServer, in MS_TEST);
            MetadataProvider.Add("PG_TEST", DataSourceType.PostgreSql, in PG_TEST);

            DaJetHost host = DaJetHost.Create("scripts").Run();

            ExecuteScriptAsync(in host);

            //ExecuteScriptSync(in host);

            Console.WriteLine("Press any key to continue ...");
            ConsoleKeyInfo key = Console.ReadKey(false);

            if (key.KeyChar == 'r')
            {
                ExecuteScriptAsync(in host);
            }
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

        private static DataObject GetParametersFromJson()
        {
            string filePath = Path.Combine(AppContext.BaseDirectory, "scripts", "parameters.json");

            using (StreamReader reader = new(filePath, Encoding.UTF8))
            {
                string json = reader.ReadToEnd();

                return JsonSerializer.Deserialize<DataObject>(json, JsonOptions);
            }
        }
        private static void ExecuteScriptSync(in DaJetHost host)
        {
            string source;
            string scriptPath = "array\\ms_select.djs";
            string filePath = Path.Combine(AppContext.BaseDirectory, "scripts", scriptPath);

            using (StreamReader reader = new(filePath, Encoding.UTF8))
            {
                source = reader.ReadToEnd();
            }

            Console.WriteLine(filePath);
            Console.WriteLine(source);
            Console.WriteLine("----");

            string json;

            DataObject parameters = new();

            parameters.SetValue("Массив", new List<string>() { "MS-01", "MS-02", "MS-03" });

            Script script = new ScriptBuilder().FromSource(in source).Build();

            Console.WriteLine("INPUT SCHEMA");
            JsonObject input = script.GetInputJsonSchema();
            json = JsonSerializer.Serialize(input, JsonOptions);
            Console.WriteLine(json);
            Console.WriteLine("---");

            Console.WriteLine("OUTPUT SCHEMA");
            JsonObject output = script.GetOutputJsonSchema();
            json = JsonSerializer.Serialize(output, JsonOptions);
            Console.WriteLine(json);
            Console.WriteLine("---");
            
            object value = host.Run("array/ms_select.djs", in parameters);

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

        private static void ExecuteScriptAsync(in DaJetHost host)
        {
            Task<object> task = host.RunAsync("select/simple.djs");

            Task show = task.ContinueWith(ShowAsyncResult);

            //host.Cancel(task.Id);

            show.Wait();

            //object value = task.Result;

            //string json = JsonSerializer.Serialize(value, value.GetType(), JsonOptions);

            //Console.WriteLine(json);
        }
        private static void ShowAsyncResult(Task<object> task)
        {
            if (task.IsCompletedSuccessfully)
            {
                object value = task.Result;

                if (value is not null)
                {
                    string json = JsonSerializer.Serialize(value, value.GetType(), JsonOptions);

                    Console.WriteLine(json);
                }
                else
                {
                    Console.WriteLine($"Task [{task.Id}] returned null value.");
                }
            }
            else if (task.IsCanceled)
            {
                Console.WriteLine($"Task [{task.Id}] is canceled.");
            }
            else
            {
                Console.WriteLine($"Task [{task.Id}] is faulted: {task.Exception?.Message}");
            }
        }
    }
}