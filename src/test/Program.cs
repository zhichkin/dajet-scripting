using DaJet.Json;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace test
{
    internal class Program
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        static void Main(string[] args)
        {
            JsonOptions.Converters.Add(new DataTypeJsonConverter());
            JsonOptions.Converters.Add(new JsonStringEnumConverter());

            //TestDeclareStatement();

            TestScriptBinding();
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
            string sourceCode = "";

            Parser parser = new();

            if (!parser.TryParse(in sourceCode, out Script script, out string error))
            {
                Console.WriteLine(error); return;
            }

            ISchemaProvider schema = null; //TODO: init provider

            Binder binder = new();

            if (!binder.TryBind(script, in schema, out Scope scope, out List<string> errors))
            {
                Console.WriteLine(string.Join('\n', errors)); return;
            }

            string json = JsonSerializer.Serialize(script, JsonOptions);

            Console.WriteLine(json);
        }
    }
}