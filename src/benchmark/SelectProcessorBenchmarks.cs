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
    [IterationCount(100)]
    //[MinIterationCount(1000)]
    //[MaxIterationCount(20)]
    public class SelectProcessorBenchmarks
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

        private static string _ms_script;
        private static string _pg_script;
        private static ScriptProcessor _processor;

        [GlobalSetup]
        public void GlobalSetup()
        {
            MetadataProvider.Add("MS_TEST", DataSourceType.SqlServer, in MS_TEST);
            MetadataProvider.Add("PG_TEST", DataSourceType.PostgreSql, in PG_TEST);

            string ms_file = Path.Combine(AppContext.BaseDirectory, "scripts", "ms_simple.djs");
            using (StreamReader reader = new(ms_file, Encoding.UTF8))
            {
                _ms_script = reader.ReadToEnd();
            }

            string pg_file = Path.Combine(AppContext.BaseDirectory, "scripts", "pg_simple.djs");
            using (StreamReader reader = new(pg_file, Encoding.UTF8))
            {
                _pg_script = reader.ReadToEnd();
            }

            Compiler compiler = new();

            _processor = compiler.Compile(in _ms_script);

            
        }
        [GlobalCleanup]
        public void GlobalCleanup()
        {
            
        }
        
        //[Benchmark(Description = "Compile")]
        //public bool CompileNoRun()
        //{
        //    Compiler compiler = new();

        //    _processor = compiler.Compile(in _script);

        //    return true;
        //}

        //[Benchmark(Description = "Compile and run")]
        //public bool CompileAndRun()
        //{
        //    Compiler compiler = new();

        //    _processor = compiler.Compile(in _script);

        //    _processor.Execute();

        //    return true;
        //}

        [Benchmark(Description = "MS Compiled")]
        public object MS_Compiled()
        {
            _processor.Execute();

            return _processor.ReturnValue;
        }

        [Benchmark(Description = "MS Interpreter")]
        public object MS_Interpreter()
        {
            Interpreter executor = new(in _ms_script);

            object value = executor.Execute();

            return value;
        }

        [Benchmark(Description = "PG Interpreter")]
        public object PG_Interpreter()
        {
            Interpreter executor = new(in _pg_script);

            object value = executor.Execute();

            return value;
        }
    }
}