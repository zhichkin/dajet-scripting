using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using DaJet.Data;
using DaJet.Metadata;
using DaJet.Scripting;
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

        private static string _script;
        private static ScriptProcessor _processor;

        [GlobalSetup]
        public void GlobalSetup()
        {
            MetadataProvider.Add("MS_TEST", DataSourceType.SqlServer, in MS_TEST);

            string filePath = Path.Combine(AppContext.BaseDirectory, "scripts", "select-complex-object.djs");

            using (StreamReader reader = new(filePath, Encoding.UTF8))
            {
                _script = reader.ReadToEnd();
            }

            Compiler compiler = new();

            _processor = compiler.Compile(in _script);
        }
        [GlobalCleanup]
        public void GlobalCleanup()
        {
            
        }
        [Benchmark(Description = "Compile and run")]
        public bool CompileAndRun()
        {
            Compiler compiler = new();

            _processor = compiler.Compile(in _script);

            _processor.Execute();

            return true;
        }

        [Benchmark(Description = "Run compiled")]
        public bool ExecuteCompiled()
        {
            _processor.Execute();

            return true;
        }
    }
}