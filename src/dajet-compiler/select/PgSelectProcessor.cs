using DaJet.Data;
using Npgsql;

namespace DaJet.Scripting
{
    public abstract class PgSelectProcessor : ProcessorBase
    {
        protected readonly ScriptProcessor _script;
        protected PgSelectProcessor(ScriptProcessor script)
        {
            ArgumentNullException.ThrowIfNull(script, nameof(script));

            _script = script;

            Initialize(); // initialize SqlCommand
        }
        protected abstract void Configure(NpgsqlCommand command); // input
        protected abstract void Process(NpgsqlDataReader reader); // output
        public string SqlCommand { get; set; }
        public override void Execute()
        {
            //IsCancellationRequested = false;

            int processed = 0; //TODO: @@ROWCOUNT

            PgDataSource source = _script.GetPgDataSource();

            using (NpgsqlCommand command = source.CreateCommand())
            {
                command.CommandText = SqlCommand;

                Configure(command);

                try
                {
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read()) // !IsCancellationRequested
                        {
                            Process(reader); processed++; // System.Diagnostics.Metrics !?
                        }

                        reader.Close();
                    }

                    if (processed > 0)
                    {
                        Synchronize();
                    }
                }
                //catch (Exception error)
                //{
                //    try
                //    {
                //        source.TxRollback(); throw;
                //    }
                //    catch
                //    {
                //        throw error;
                //    }
                //}
                finally
                {
                    //Cancel(); ?
                }
            }
        }
        public override void Dispose()
        {
            //IsCancellationRequested = true;
        }
        protected virtual void Synchronize()
        {
            // submit transaction batch or throw

            // ISynchronizable.Synchronize();
        }
    }
}