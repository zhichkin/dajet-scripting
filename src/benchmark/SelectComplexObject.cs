using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using DaJet.Data;
using DaJet.Metadata;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using System.Text;

namespace benchmark
{
    [Config(typeof(Config))]
    [MemoryDiagnoser]
    [MinColumn, MaxColumn]
    //[WarmupCount(1)]
    //[IterationCount(10)]
    //[MinIterationCount(5)]
    //[MaxIterationCount(20)]
    public class SelectComplexObject
    {
        private static readonly string MS_TEST = "Data Source=ZHICHKIN;Initial Catalog=dajet-metadata;Integrated Security=True;Encrypt=False;";
        private static readonly string PG_TEST = "Host=127.0.0.1;Port=5432;Database=dajet-metadata;Username=postgres;Password=postgres;";
        private class Config : ManualConfig
        {
            public Config()
            {
                AddJob(Job.Default.WithGcServer(true).WithGcForce(false).WithId("Server"));
                //AddJob(Job.Default.WithGcServer(false).WithGcForce(false).WithId("Workstation));

                //AddJob(Job.Default.WithGcServer(true).WithGcForce(true).WithId("ServerForce"));
                //AddJob(Job.Default.WithGcServer(false).WithGcForce(true).WithId("WorkstationForce""));
            }
        }

        private static ScriptProcessor _processor;

        [GlobalSetup]
        public void GlobalSetup()
        {
            MetadataProvider.Add("MS_TEST", DataSourceType.SqlServer, in MS_TEST);

            string source;
            string filePath = Path.Combine(AppContext.BaseDirectory, "scripts", "select-complex-object.djs");

            using (StreamReader reader = new(filePath, Encoding.UTF8))
            {
                source = reader.ReadToEnd();
            }

            Parser parser = new();

            if (!parser.TryParse(in source, out Script script, out string error))
            {
                throw new InvalidOperationException(error);
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
                throw new InvalidOperationException(error);
            }

            Binder binder = new();
            OneDbSchemaProvider schema = new();

            if (!binder.TryBind(in script, schema, out List<string> errors))
            {
                throw new InvalidOperationException(string.Join('\n', errors));
            }

            SqlTranspiler transpiler = new("SqlServer", 2000);

            if (!transpiler.TryTranspile(script, out List<SqlStatement> statements, out errors))
            {
                throw new InvalidOperationException(string.Join('\n', errors));
            }

            Compiler compiler = new();

            _processor = compiler.Compile(in script, in statements);
        }
        [GlobalCleanup]
        public void GlobalCleanup()
        {
            
        }

        [Benchmark(Description = "SELECT complex object")]
        public bool ExecuteScript()
        {
            _processor.Execute();

            return true;
        }
    }
}