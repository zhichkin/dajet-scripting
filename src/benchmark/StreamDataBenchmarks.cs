using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using DaJet.Data;
using DaJet.Host;
using DaJet.Metadata;

namespace benchmark
{
    [Config(typeof(Config))]
    [MemoryDiagnoser]
    [MinColumn, MaxColumn]
    //[WarmupCount(1)]
    [IterationCount(10)]
    public class StreamDataBenchmarks
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

        private static DaJetHost _host;

        [GlobalSetup]
        public void GlobalSetup()
        {
            MetadataProvider.Add("MS_TEST", DataSourceType.SqlServer, in MS_TEST);
            MetadataProvider.Add("PG_TEST", DataSourceType.PostgreSql, in PG_TEST);

            _host = DaJetHost.Create("scripts");
        }
        [GlobalCleanup]
        public void GlobalCleanup()
        {
            
        }
        
        [Benchmark(Description = "STREAM MS > PG")]
        public object STREAM_MS_PG()
        {
            //return _host.Run("stream-ms-pg.djs");

            Task<object> task = _host.RunAsync("stream-ms-pg.djs");

            task.Wait();

            return task.Result;
        }

        [Benchmark(Description = "STREAM PG > MS")]
        public object STREAM_PG_MS()
        {
            //return _host.Run("stream-ms-pg.djs");

            Task<object> task = _host.RunAsync("stream-pg-ms.djs");

            task.Wait();

            return task.Result;
        }
    }
}