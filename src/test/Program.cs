using DaJet.Scripting;
using DaJet.Scripting.Model;

namespace test
{
    internal class Program
    {
        static void Main(string[] args)
        {
            TestDeclareStatement();


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
    }
}