using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using DaJet.Data;
using DaJet.Host;
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
    [IterationCount(100)]
    //[MinIterationCount(1000)]
    //[MaxIterationCount(20)]
    public class SelectProcessorBenchmarks
    {
        private static readonly string MS_TEST = "Data Source=Z-NOTEBOOK;Initial Catalog=test;Integrated Security=True;Encrypt=False;";
        private static readonly string PG_TEST = "Host=127.0.0.1;Port=5432;Database=test;Username=postgres;Password=postgres;";
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

        private static Interpreter _ms_executor;
        private static Interpreter _pg_executor;
        private static ScriptProcessor _processor;
        private static DaJetHost _host;

        [GlobalSetup]
        public void GlobalSetup()
        {
            MetadataProvider.Add("MS_TEST", DataSourceType.SqlServer, in MS_TEST);
            MetadataProvider.Add("PG_TEST", DataSourceType.PostgreSql, in PG_TEST);

            _host = DaJetHost.Create("scritps");

            string ms_file = Path.Combine(AppContext.BaseDirectory, "scripts", "ms_simple.djs");
            Script ms_script = new ScriptBuilder().FromFile(in ms_file).Build();
            _ms_executor = new Interpreter(in ms_script);

            string pg_file = Path.Combine(AppContext.BaseDirectory, "scripts", "pg_simple.djs");
            Script pg_script = new ScriptBuilder().FromFile(in ms_file).Build();
            _pg_executor = new Interpreter(in pg_script);

            using (StreamReader reader = new(ms_file, Encoding.UTF8))
            {
                _processor = new Compiler().Compile(reader.ReadToEnd());
            }
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
            return _ms_executor.Execute();
        }

        [Benchmark(Description = "PG Interpreter")]
        public object PG_Interpreter()
        {
            return _pg_executor.Execute();
        }

        [Benchmark(Description = "MS Host")]
        public object MS_Host_Async()
        {
            Task<object> task = _host.RunAsync("ms_simple.djs");

            task.Wait();

            return task.Result;
        }

        [Benchmark(Description = "PG Host")]
        public object PG_Host_Async()
        {
            Task<object> task = _host.RunAsync("pg_simple.djs");

            task.Wait();

            return task.Result;
        }
    }
}