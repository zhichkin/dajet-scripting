using DaJet.Data;
using DaJet.Json;
using DaJet.Metadata;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using DaJet.TypeSystem;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using static Npgsql.Replication.PgOutput.Messages.RelationMessage;

namespace test
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
            string NewLine = Environment.NewLine;
            string source = "DECLARE @Ссылка entity"
                + NewLine + "DECLARE @Код    string = '0001'"
                + NewLine + "DECLARE @Запись object"
                + NewLine + "USE 'MS_TEST'"
                + NewLine + "SELECT Ссылка"
                + NewLine + "     , Код"
                + NewLine + "     , ВерсияДанных"
                + NewLine + "     , ПометкаУдаления"
                + NewLine + "     , Наименование"
                + NewLine + "     , Сумма = SUM(Наименование) OVER ()"
                + NewLine + "INTO @Запись"
                + NewLine + "FROM Справочник.Справочник1"
                + NewLine + "WHERE Ссылка = @Ссылка AND Код = @Код"
                + NewLine + "END";

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

                if (statement.Node is SelectStatement select)
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
    }
}