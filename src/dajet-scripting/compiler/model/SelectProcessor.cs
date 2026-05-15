using DaJet.Data;
using Microsoft.Data.SqlClient;
using System.Buffers.Binary;

namespace DaJet.Scripting
{
    public abstract class SelectProcessor : ProcessorBase
    {
        protected readonly static byte[] TRUE = [0x01];
        protected readonly static byte[] FALSE = [0x00];
        
        protected readonly ScriptProcessor _script;
        protected SelectProcessor(ScriptProcessor script)
        {
            ArgumentNullException.ThrowIfNull(script, nameof(script));

            _script = script;

            Initialize(); // initialize SqlCommand
        }
        protected abstract void Configure(SqlCommand command); // input
        protected abstract void Process(SqlDataReader reader); // output
        internal int YearOffset { get; set; }
        public string SqlCommand { get; set; }
        public override void Execute()
        {
            //IsCancellationRequested = false;

            int processed = 0; //TODO: @@ROWCOUNT

            MsDataSource source = _script.GetMsDataSource();

            using (SqlCommand command = source.CreateCommand())
            {
                command.CommandText = SqlCommand;

                Configure(command);

                try
                {
                    using (SqlDataReader reader = command.ExecuteReader())
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