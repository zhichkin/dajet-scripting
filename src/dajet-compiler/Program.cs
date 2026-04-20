using DaJet.Data;
using DaJet.Json;
using DaJet.Metadata;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace DaJet.Compiler
{
    internal class Program
    {
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
            //Test(); return;

            JsonOptions.Converters.Add(new DataTypeJsonConverter());
            JsonOptions.Converters.Add(new JsonStringEnumConverter());

            //TestDeclareStatement();

            //TestScriptBinding();

            TestCompiler();
        }
        private static void Test()
        {
            string source = "d.zhichkin";
            Console.WriteLine(source);
            string encoded = UrlEncoder.Create(UnicodeRanges.All).Encode(source);
            Console.WriteLine(encoded);
        }

        //private static void ActivateStreams(in string path)
        //{
        //    foreach (string file in Directory.EnumerateFiles(path, DAJET_SCRIPT_FILE_EXTENSION))
        //    {
        //        ActivateStream(in file);
        //    }

        //    foreach (string catalog in Directory.EnumerateDirectories(path))
        //    {
        //        ActivateStreams(in catalog);
        //    }
        //}
        //private static void ActivateStream(in string file)
        //{
        //    if (_streams.ContainsKey(file)) { return; }

        //    if (!File.Exists(file)) { return; }

        //    string script;

        //    using (StreamReader reader = new(file, Encoding.UTF8))
        //    {
        //        script = reader.ReadToEnd();
        //    }

        //    Stopwatch watch = new();

        //    watch.Start();

        //    Dictionary<string, object> parameters = new();

        //    if (StreamFactory.TryCreateStream(in script, in parameters, out IProcessor stream, out string error))
        //    {
        //        _ = Task.Factory.StartNew(stream.Process, TaskCreationOptions.LongRunning);

        //        _ = _streams.TryAdd(file, stream);

        //        watch.Stop();

        //        FileLogger.Default.Write($"[STREAM][Assembled in {watch.ElapsedMilliseconds} ms] {file}");
        //    }
        //    else
        //    {
        //        FileLogger.Default.Write($"[ERROR] {file}");
        //        FileLogger.Default.Write(error);
        //    }
        //}

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

        private static void TestCompiler()
        {
            OneDbSchemaProvider schema = new();
            MetadataProvider.Add("MS_TEST", DataSourceType.SqlServer, in MS_TEST);
            MetadataProvider.Add("PG_TEST", DataSourceType.PostgreSql, in PG_TEST);

            string source;
            string filePath = $"{AppContext.BaseDirectory}scripts\\select_simple.djs";

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

            Binder binder = new();

            if (!binder.TryBind(in script, schema, out List<string> errors))
            {
                Console.WriteLine(string.Join('\n', errors)); return;
            }

            SqlTranspiler transpiler = new("SqlServer", 2000);

            if (!transpiler.TryTranspile(script, out List<SqlStatement> statements, out errors))
            {
                Console.WriteLine(string.Join('\n', errors)); return;
            }

            if (statements is not null && statements.Count > 0)
            {
                Console.WriteLine(statements[0].Sql);
                Console.WriteLine("----");
            }

            Compiler compiler = new();
            ScriptProcessor processor = compiler.Compile(in script, in statements);

            if (processor is not null)
            {
                processor.Execute();

                Console.WriteLine(processor.GetType());

                string json = JsonSerializer.Serialize(processor, processor.GetType(), JsonOptions);

                Console.WriteLine(json);
            }
            else
            {
                Console.WriteLine("Compile and save");
            }
        }
    }
}