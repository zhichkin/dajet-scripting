using DaJet.Data;
using DaJet.Json;
using DaJet.Metadata;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using System.Timers;

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
        private static DaJetHost _host;
        private static System.Timers.Timer _heartbeat;
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

            //_host = DaJetHost.Create("scripts").Run(); Heartbeat();
            DaJetHost host = DaJetHost.Create("scripts").Run();
            //DaJetHost host = DaJetHost.Create("scripts").ReadOnly().Run();

            //DataObject parameters = new();
            //parameters.SetValue("Отправитель", "PG_TEST");
            //parameters.SetValue("Получатель",  "MS_TEST");
            //_ = host.RunAsync("exchange/stream-pg-ms.djs", in parameters).ContinueWith(ShowAsyncResult);

            _ = host.RunAsync("consume/ms/simple-stream.djs").ContinueWith(ShowAsyncResult);
            //_ = host.RunAsync("consume/ms/longrunning-stream.djs").ContinueWith(ShowAsyncResult);
            //_ = host.RunAsync("consume/ms/queue-table-stream.djs").ContinueWith(ShowAsyncResult);
            //_ = host.RunAsync("consume/ms/change-tracking.djs").ContinueWith(ShowAsyncResult);

            Console.WriteLine("Press any key to continue ..."); _ = Console.ReadKey(true);
        }
        private static void ShowAsyncResult(Task<object> task)
        {
            if (task.IsCompletedSuccessfully)
            {
                object value = task.Result;

                if (value is not null)
                {
                    string json = JsonSerializer.Serialize(value, value.GetType(), JsonOptions);

                    Console.WriteLine($"Task [{task.Id}] return value:");
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
                Exception error = task.Exception.Flatten().InnerException;

                Console.WriteLine($"Task [{task.Id}] is faulted: {error.Message}");
            }
        }
        private static void Heartbeat()
        {
            System.Timers.Timer timer = new();

            if (Interlocked.CompareExchange(ref _heartbeat, timer, null) is not null)
            {
                timer.Dispose();
            }
            else
            {
                _heartbeat.AutoReset = true;
                _heartbeat.Elapsed += ShowRunningTasks;
                _heartbeat.Interval = TimeSpan.FromSeconds(1).TotalMilliseconds;
            }

            _heartbeat.Start();
        }
        private static void ShowRunningTasks(object sender, ElapsedEventArgs args)
        {
            foreach (RunningTaskStatus status in _host.GetRunningTasks())
            {
                Console.WriteLine(status.ToString());
            }
        }
    }
}